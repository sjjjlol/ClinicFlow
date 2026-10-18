using System;
using System.Linq;
using System.Threading.Tasks;
using ClinicFlow;
using ClinicFlow.Ois;
using ClinicFlow.Ois.Requests;
using ClinicFlow.Ois.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySqlConnector;

namespace ClinicFlow.Tools
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var connection = Environment.GetEnvironmentVariable("UPGRADE_DB") ?? throw new Exception("UPGRADE_DB required");
            if (new MySqlConnectionStringBuilder(connection).Database != "clinicflow_upgrade_tests")
                throw new Exception("Refusing non-drill database");
            await using var db = new ClinicDb(new DbContextOptionsBuilder<ClinicDb>()
                .UseMySql(connection, new MySqlServerVersion(new Version(8, 4, 8))).Options);
            const string previous = "20261013081253_OisWorkflow";
            var migrator = db.GetService<IMigrator>();
            var mode = args.Single();
            if (mode == "prepare")
            {
                await db.Database.EnsureDeletedAsync();
                await migrator.MigrateAsync(previous);
                await db.Database.ExecuteSqlRawAsync("INSERT INTO Users (Id,Role,PasswordHash) VALUES ('retired-admin','Admin','unused'),('retained-clinician','Clinician','preserve-hash')");
                // Seed using the previous wire/schema contract, not today's EF entities and service.
                const string caseId = "upgrade-case";
                var json = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
                var legacyCommand = new { Id = "", Version = 0, PatientId = 1, CaseId = "", PlanId = "", ResourceId = "", Kind = "", StartUtc = (string?)null, EndUtc = (string?)null, Text = "Preserved OIS case", Diagnosis = "", Reason = "", Attachment = "", Outcome = "", FailureType = "", IdentityChecked = false, Available = true };
                string Hash(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
                var key = Hash(System.Text.Json.JsonSerializer.Serialize(new[] { "ois", "retained-clinician", "Clinician", "upgrade-preserve" }));
                var fingerprint = Hash(System.Text.Json.JsonSerializer.Serialize(new { op = "case", command = legacyCommand }, json));
                var response = System.Text.Json.JsonSerializer.Serialize(new { targetId = caseId });
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO CareCase (Id, Version, PatientId, Title) VALUES ({caseId}, 1, 1, 'Preserved OIS case')");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO OisAudit (Actor, Role, Action, TargetId, Detail, CorrelationId, AtUtc) VALUES ('retained-clinician', 'Clinician', 'case', {caseId}, '{{}}', 'upgrade-drill', UTC_TIMESTAMP())");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Idempotency (Id, Fingerprint, Response, CreatedUtc) VALUES ({key}, {fingerprint}, {response}, UTC_TIMESTAMP())");
                Console.WriteLine("Prepared previous OIS schema with clinical data, audit, replay result and staff account.");
            }
            else if (mode == "upgrade")
            {
                await migrator.MigrateAsync();
                if (await db.Users.AnyAsync(x => x.Id == "retired-admin")) throw new Exception("Obsolete account survived");
                await using var command = db.Database.GetDbConnection().CreateCommand();
                await db.Database.OpenConnectionAsync();
                command.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name IN ('Appointments','SlotClaims','Tasks','Audits','Resources','Outbox','SyncAttempt','ImagingIdentity','ImagingLink','ImagingAudit')";
                if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) throw new Exception("Obsolete tables survived");
                // Existing OIS request keys still replay, without duplicating a case or audit.
                await new CommandExecutor(db, TimeProvider.System).Execute(new CreateCaseRequest { PatientId = 1, Text = "Preserved OIS case" },
                    "retained-clinician", "Clinician", "upgrade-preserve", "upgrade-drill", default,
                    (state, input, actor, role, now) => new CasesOperations(state).CreateCase(input, actor, role, now));
            }
            else if (mode != "verify-old") throw new Exception("prepare | upgrade | verify-old");
            if (await db.Set<CareCase>().CountAsync() != 1
                || (await db.Set<CareCase>().Select(x => x.Title).SingleAsync()) != "Preserved OIS case"
                || await db.Set<OisAudit>().CountAsync() != 1 || await db.Idempotency.CountAsync() != 1
                || (await db.Users.SingleAsync(x => x.Id == "retained-clinician")).PasswordHash != "preserve-hash"
                || await db.Patients.CountAsync() != 2)
                throw new Exception("OIS preservation failed");
            var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            if (mode == "verify-old" && (migrations.Last() != previous || !await db.Users.AnyAsync(x => x.Id == "retired-admin")))
                throw new Exception("Previous backup was not restored");
            Console.WriteLine($"PASS {mode}: OIS case, patients, staff, audit and idempotency preserved; latest={migrations.Last()}");
        }
    }
}

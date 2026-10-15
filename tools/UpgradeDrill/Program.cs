using ClinicFlow;
using ClinicFlow.Ois;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySqlConnector;

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
    await new OisService(db, TimeProvider.System).Execute("case", new(PatientId: 1, Text: "Preserved OIS case"),
        "retained-clinician", "Clinician", "upgrade-preserve", "upgrade-drill", default);
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
    await new OisService(db, TimeProvider.System).Execute("case", new(PatientId: 1, Text: "Preserved OIS case"),
        "retained-clinician", "Clinician", "upgrade-preserve", "upgrade-drill", default);
}
else if (mode != "verify-old") throw new Exception("prepare | upgrade | verify-old");
if (await db.Set<CareCase>().CountAsync() != 1
    || (await db.Set<CareCase>().SingleAsync()).Title != "Preserved OIS case"
    || await db.Set<OisAudit>().CountAsync() != 1 || await db.Idempotency.CountAsync() != 1
    || (await db.Users.SingleAsync(x=>x.Id=="retained-clinician")).PasswordHash != "preserve-hash"
    || await db.Patients.CountAsync() != 2)
    throw new Exception("OIS preservation failed");
var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
if (mode == "verify-old" && (migrations.Last() != previous || !await db.Users.AnyAsync(x=>x.Id=="retired-admin")))
    throw new Exception("Previous backup was not restored");
Console.WriteLine($"PASS {mode}: OIS case, patients, staff, audit and idempotency preserved; latest={migrations.Last()}");

using ClinicFlow;
using ClinicFlow.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySqlConnector;

var connection =
    Environment.GetEnvironmentVariable("UPGRADE_DB") ?? throw new Exception("UPGRADE_DB required");
if (new MySqlConnectionStringBuilder(connection).Database != "clinicflow_upgrade_tests")
    throw new Exception("Refusing non-drill database");
await using var db = new ClinicDb(
    new DbContextOptionsBuilder<ClinicDb>()
        .UseMySql(connection, new MySqlServerVersion(new Version(8, 4, 8)))
        .Options
);
const string previous = "20260923164608_SyncAttempts";
var migrator = db.GetService<IMigrator>();
if (args[0] == "prepare")
{
    await db.Database.EnsureDeletedAsync();
    await migrator.MigrateAsync(previous);
    var start = new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);
    await new SchedulingService(db, new NoTransactionProbe()).Create(
        new(1, 1, start, start.AddHours(1)),
        "upgrade-demo",
        "preserve-me",
        "upgrade-drill",
        default
    );
    Console.WriteLine(
        "Prepared old schema with one appointment, four claims, two tasks, one audit, one outbox and idempotency result."
    );
}
else if (args[0] == "upgrade")
{
    var before = (await db.Appointments.AsNoTracking().SingleAsync()).Id;
    await migrator.MigrateAsync();
    db.ChangeTracker.Clear();
    if ((await db.Appointments.SingleAsync()).Id != before)
        throw new Exception("Data changed");
    Console.WriteLine(
        "Applied additive ResourceScheduleIndex migration; appointment identity preserved."
    );
}
else if (args[0] != "verify-old")
    throw new Exception("prepare | upgrade | verify-old");
if (
    await db.Appointments.CountAsync() != 1
    || await db.SlotClaims.CountAsync() != 4
    || await db.Tasks.CountAsync() != 2
    || await db.Audits.CountAsync() != 1
    || await db.Outbox.CountAsync() != 1
    || await db.Idempotency.CountAsync() != 1
)
    throw new Exception("Row preservation failed");
var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
if (args[0] == "verify-old" && migrations.Last() != previous)
    throw new Exception("Old backup was not restored");
Console.WriteLine(
    $"{DateTime.UtcNow:O}: {args[0]} PASS; migrations={migrations.Length}; latest={migrations.Last()}; all six entity row counts preserved."
);

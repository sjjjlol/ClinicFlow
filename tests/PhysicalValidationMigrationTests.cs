using System;
using System.Linq;
using System.Threading.Tasks;
using ClinicFlow.Ois;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace ClinicFlow.Tests
{
    public sealed class PhysicalValidationMigrationTests : IAsyncLifetime
    {
        private readonly DatabaseFixture fixture = new DatabaseFixture();
        private const string Previous = "20261016074638_StructuredVerification";
        public Task InitializeAsync() => fixture.InitializeAsync();
        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task Upgrade_and_downgrade_preserve_each_signed_result_and_legacy_workspace_shape()
        {
            await using var db = fixture.Db();
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(Previous);
            // Seed the retired schema directly: this must not depend on today's domain model.
            foreach (var id in new[] { "failed", "passed", "pending" })
            {
                var outcome = id == "pending" ? "" : id == "failed" ? "Failed" : "Passed";
                var failure = id == "failed" ? "Equipment" : "";
                var signedBy = id == "pending" ? "" : "physicist";
                DateTime? signedUtc = id == "pending" ? null : new DateTime(2030, 1, 1, 10, 0, 0, DateTimeKind.Utc);
                var status = id == "pending" ? "Booked" : "Completed";
                var note = id == "pending" ? "" : "历史结论：" + id;
                var measured = id == "pending" ? "" : "测量值：" + id;
                var attachment = id == "pending" ? "" : "ref:" + id;
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO Reservation (Id, Version, Kind, ResourceId, StartUtc, EndUtc, Status, Reason,
                        ValidationOutcome, ValidationNote, FailureType, SignedBy, SignedUtc, MeasuredValues, ResultAttachment)
                    VALUES ({id}, 2, 'Experiment', 'linac-a', '2030-01-01 09:00:00', '2030-01-01 10:00:00', {status}, '',
                        {outcome}, {note}, {failure}, {signedBy}, {signedUtc}, {measured}, {attachment})");
            }
            await migrator.MigrateAsync();
            var results = await db.Set<PhysicalValidation>().AsNoTracking().OrderBy(x => x.ReservationId).ToListAsync();
            Assert.Equal(2, results.Count);
            Assert.Equal("Failed", results[0].ValidationOutcome);
            Assert.Equal("Equipment", results[0].FailureType);
            Assert.Equal("Passed", results[1].ValidationOutcome);
            Assert.All(results, result =>
            {
                Assert.Equal("历史结论：" + result.ReservationId, result.ValidationNote);
                Assert.Equal("测量值：" + result.ReservationId, result.MeasuredValues);
                Assert.Equal("ref:" + result.ReservationId, result.ResultAttachment);
                Assert.Equal("physicist", result.SignedBy);
                Assert.Equal(new DateTime(2030, 1, 1, 10, 0, 0), result.SignedUtc);
            });
            var snapshot = await new WorkspaceQuery(db).Snapshot("physicist", default);
            var passed = snapshot.Reservations.Single(x => x.Reservation.Id == "passed").Reservation;
            Assert.Equal("Passed", passed.ValidationOutcome);
            Assert.Equal("ref:passed", passed.ResultAttachment);
            Assert.Equal("", snapshot.Reservations.Single(x => x.Reservation.Id == "pending").Reservation.ValidationOutcome);
            // Reverse then reapply: all seven fields must survive both directions.
            db.ChangeTracker.Clear();
            await migrator.MigrateAsync(Previous);
            await migrator.MigrateAsync();
            var again = await db.Set<PhysicalValidation>().AsNoTracking().OrderBy(x => x.ReservationId).ToListAsync();
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(results), System.Text.Json.JsonSerializer.Serialize(again));
        }
    }
}

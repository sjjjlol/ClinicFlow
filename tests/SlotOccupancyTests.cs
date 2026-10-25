using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClinicFlow.Ois;
using ClinicFlow.Application.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace ClinicFlow.Tests
{
    public sealed class SlotOccupancyTests : IAsyncLifetime
    {
        private readonly DatabaseFixture fixture = new DatabaseFixture();
        private static readonly DateTime Start = new DateTime(2035, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        public Task InitializeAsync() => fixture.InitializeAsync();
        public Task DisposeAsync() => Task.CompletedTask;
        private async Task<string> Send<T>(T request, Func<OisSession, T, string, string, DateTime, string> handler,
            string role = "Scheduler", string? key = null) where T : class
        {
            await using var db = fixture.Db();
            var result = await new CommandExecutor(new EfCommandTransaction(db), TimeProvider.System).Execute(
                request, role.ToLowerInvariant(), role, key ?? Guid.NewGuid().ToString(), "slots", default, handler);
            return JsonSerializer.SerializeToElement(result).GetProperty("targetId").GetString()!;
        }
        private BookConsultationCommand Consultation(int patient = 1, DateTime? start = null) => new BookConsultationCommand
        { PatientId = patient, ResourceId = "clinician", StartUtc = start ?? Start, EndUtc = (start ?? Start).AddMinutes(30) };
        private Task<string> Book(int patient = 1, DateTime? start = null) => Send(Consultation(patient, start),
            (s, c, a, r, n) => s.Scheduling.BookConsultation(c, a, r, n));

        [Fact]
        public async Task Cancel_releases_slots_restore_reclaims_and_reschedule_preserves_common_slots()
        {
            var id = await Book();
            await using var db = fixture.Db();
            Assert.Equal(4, await db.Set<ReservationOccupancy>().CountAsync());
            await Send(new RescheduleReservationCommand { Id = id, Version = 1, ResourceId = "clinician", StartUtc = Start.AddMinutes(15), EndUtc = Start.AddMinutes(45), Reason = "move" },
                (s, c, a, r, n) => s.Scheduling.RescheduleReservation(c, a, r, n));
            var slots = await db.Set<ReservationOccupancy>().AsNoTracking().ToListAsync();
            Assert.Equal(4, slots.Count);
            Assert.All(slots, x => Assert.Contains(x.SlotStartUtc, new[] { Start.AddMinutes(15), Start.AddMinutes(30) }));
            await Send(new CancelReservationCommand { Id = id, Version = 2, Reason = "cancel" },
                (s, c, a, r, n) => s.Scheduling.CancelReservation(c, a, r, n));
            Assert.Empty(await db.Set<ReservationOccupancy>().ToListAsync());
            await Send(new RestoreReservationCommand { Id = id, Version = 3, Reason = "restore" },
                (s, c, a, r, n) => s.Scheduling.RestoreReservation(c, a, r, n));
            Assert.Equal(4, await db.Set<ReservationOccupancy>().CountAsync());
            Assert.Equal(4, (await db.Set<Reservation>().AsNoTracking().SingleAsync()).Version);
        }

        [Fact]
        public async Task Failed_move_after_database_slot_collision_restores_old_slots_audit_and_request_key()
        {
            var id = await Book();
            var request = new RescheduleReservationCommand { Id = id, Version = 1, ResourceId = "clinician", StartUtc = Start.AddHours(1), EndUtc = Start.AddHours(1).AddMinutes(30), Reason = "move" };
            // Simulate a competing writer after the command has loaded its state. Deliberately bypass
            // the advisory locks/precheck to prove the database constraint is the final authority.
            var error = await Assert.ThrowsAsync<BusinessException>(() => Send(request, (s, c, a, r, n) =>
            {
                using var competitor = fixture.Db();
                var other = Reservation.Book("Consultation", "clinician", Start.AddHours(1), Start.AddHours(1).AddMinutes(30), "", 2, null, null);
                competitor.Add(other);
                competitor.AddRange(OccupancyStore.Slots(other));
                competitor.SaveChanges();
                return s.Scheduling.RescheduleReservation(c, a, r, n);
            }, key: "move"));
            Assert.Equal("slot_conflict", error.Code);
            await using (var db = fixture.Db())
            {
                var original = await db.Set<Reservation>().SingleAsync(x => x.Id == id);
                Assert.Equal(Start, original.StartUtc);
                Assert.Equal(1, original.Version);
                Assert.Equal(4, await db.Set<ReservationOccupancy>().CountAsync(x => x.ReservationId == id));
                Assert.Equal(1, await db.Set<OisAudit>().CountAsync());
                Assert.Equal(1, await db.Idempotency.CountAsync());
                var competingId = await db.Set<Reservation>().Where(x => x.Id != id).Select(x => x.Id).SingleAsync();
                await Send(new CancelReservationCommand { Id = competingId, Version = 1, Reason = "release" },
                    (s, c, a, r, n) => s.Scheduling.CancelReservation(c, a, r, n));
            }
            await Send(request, (s, c, a, r, n) => s.Scheduling.RescheduleReservation(c, a, r, n), key: "move");
            await using var verify = fixture.Db();
            Assert.Equal(Start.AddHours(1), (await verify.Set<Reservation>().SingleAsync(x => x.Id == id)).StartUtc);
            Assert.Equal(4, await verify.Set<ReservationOccupancy>().CountAsync());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Database_rejects_overlapping_resource_or_patient_even_without_application_precheck(bool sameResource)
        {
            await using (var seed = fixture.Db())
            {
                seed.Add(OisResource.Define("clinician-2", "Second", "Clinician"));
                await seed.SaveChangesAsync();
            }
            async Task<bool> Attempt(string resource, int patient, DateTime start)
            {
                await using var db = fixture.Db();
                await using var tx = await db.Database.BeginTransactionAsync();
                var reservation = Reservation.Book("Consultation", resource, start, start.AddMinutes(30), "", patient, null, null);
                db.Add(reservation);
                db.AddRange(OccupancyStore.Slots(reservation));
                try { await db.SaveChangesAsync(); await tx.CommitAsync(); return true; }
                catch (DbUpdateException) { await tx.RollbackAsync(); return false; }
            }
            var outcomes = await Task.WhenAll(Attempt("clinician", 1, Start),
                Attempt(sameResource ? "clinician" : "clinician-2", sameResource ? 2 : 1, Start.AddMinutes(15)));
            Assert.Single(outcomes, x => x);
            await using var verify = fixture.Db();
            Assert.Single(await verify.Set<Reservation>().ToListAsync());
            Assert.Equal(4, await verify.Set<ReservationOccupancy>().CountAsync());
        }

        [Fact]
        public async Task Unrelated_resources_can_commit_while_another_command_and_old_global_gate_are_locked()
        {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            await using var gateDb = fixture.Db();
            await using var gateTx = await gateDb.Database.BeginTransactionAsync();
            await gateDb.Database.ExecuteSqlRawAsync("SELECT * FROM OisGate WHERE Id=1 FOR UPDATE");
            var first = Task.Run(() => Send(new BookMaintenanceCommand { ResourceId = "linac-a", StartUtc = Start, EndUtc = Start.AddMinutes(30), Reason = "A" },
                (s, c, a, r, n) =>
                {
                    entered.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Other resource could not progress");
                    return s.Scheduling.BookMaintenance(c, a, r, n);
                }, "Physicist"));
            try
            {
                Assert.True(entered.Wait(TimeSpan.FromSeconds(10)), "Command must not wait on the retired global gate");
                await Send(new BookMaintenanceCommand { ResourceId = "linac-b", StartUtc = Start, EndUtc = Start.AddMinutes(30), Reason = "B" },
                    (s, c, a, r, n) => s.Scheduling.BookMaintenance(c, a, r, n), "Physicist").WaitAsync(TimeSpan.FromSeconds(10));
            }
            finally { release.Set(); await first; }
            await using var verify = fixture.Db();
            Assert.Equal(2, await verify.Set<Reservation>().CountAsync());
            Assert.Equal(4, await verify.Set<ReservationOccupancy>().CountAsync());
        }

        [Fact]
        public async Task Adjacent_slots_and_idempotent_replay_do_not_create_extra_claims()
        {
            var request = Consultation();
            await Send(request, (s, c, a, r, n) => s.Scheduling.BookConsultation(c, a, r, n), key: "same");
            await Send(request, (s, c, a, r, n) => throw new Exception("Must replay"), key: "same");
            await Book(2, Start.AddMinutes(30));
            await using var verify = fixture.Db();
            Assert.Equal(2, await verify.Set<Reservation>().CountAsync());
            Assert.Equal(8, await verify.Set<ReservationOccupancy>().CountAsync());
        }

        [Fact]
        public async Task Upgrade_backfills_non_cancelled_slots_and_active_execution_and_roundtrips()
        {
            await using var db = fixture.Db();
            var migrator = db.GetService<IMigrator>();
            const string previous = "20261024084147_AddIntegrationOutbox";
            await migrator.MigrateAsync(previous);
            var booked = Reservation.Book("Consultation", "clinician", Start, Start.AddMinutes(30), "", 1, null, null);
            var completed = Reservation.Book("Maintenance", "linac-a", Start, Start.AddMinutes(15), "maintenance", null, null, null);
            completed.Status = "Completed";
            var cancelled = Reservation.Book("Consultation", "clinician", Start, Start.AddMinutes(30), "", 2, null, null);
            cancelled.Cancel("cancel");
            db.Add(new CareCase { Id = "migration-case", PatientId = 1 });
            db.Add(new Prescription { Id = "migration-rx", CaseId = "migration-case" });
            db.Add(new TreatmentPlan { Id = "migration-plan", CaseId = "migration-case", PrescriptionId = "migration-rx", MachineId = "linac-b" });
            var executing = Reservation.Book("Treatment", "linac-b", Start.AddHours(1), Start.AddHours(1).AddMinutes(30), "", 1, "migration-case", null);
            executing.StartTreatment();
            db.Add(executing);
            db.Add(TreatmentDelivery.Start(executing.Id, "migration-plan", "linac-b", "clinician", Start.AddHours(1)));
            db.AddRange(booked, completed, cancelled);
            await db.SaveChangesAsync();
            await migrator.MigrateAsync();
            Assert.Equal(9, await db.Set<ReservationOccupancy>().CountAsync());
            Assert.Equal(2, await db.Set<ActiveExecution>().CountAsync());
            Assert.False(await db.Set<ReservationOccupancy>().AnyAsync(x => x.ReservationId == cancelled.Id));
            await migrator.MigrateAsync(previous);
            await migrator.MigrateAsync();
            Assert.Equal(9, await db.Set<ReservationOccupancy>().CountAsync());
            Assert.Equal(2, await db.Set<ActiveExecution>().CountAsync());
            Assert.Equal(4, await db.Set<Reservation>().CountAsync());
        }

        [Fact]
        public async Task Machine_stop_and_booking_are_serialized_without_losing_affected_tasks()
        {
            async Task<string> AttemptBooking()
            {
                try
                {
                    return await Send(new BookMaintenanceCommand { ResourceId = "linac-a", StartUtc = Start, EndUtc = Start.AddMinutes(30), Reason = "maintenance" },
                        (s, c, a, r, n) => s.Scheduling.BookMaintenance(c, a, r, n), "Physicist");
                }
                catch (BusinessException e) { Assert.Equal("machine_unavailable", e.Code); return "rejected"; }
            }
            var booking = AttemptBooking();
            var stop = Send(new SetMachineAvailabilityCommand { Id = "linac-a", Version = 1, Available = false, Reason = "stopped" },
                (s, c, a, r, n) => s.Scheduling.SetMachineAvailability(c, a, r, n), "Physicist");
            await Task.WhenAll(booking, stop);
            await using var db = fixture.Db();
            Assert.False((await db.Set<OisResource>().SingleAsync(x => x.Id == "linac-a")).Available);
            if (await booking == "rejected") Assert.Empty(await db.Set<ReservationOccupancy>().ToListAsync());
            else Assert.True(await db.Set<OisTask>().AnyAsync(x => x.TargetId == booking.Result));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Upgrade_refuses_invalid_or_overlapping_history(bool overlap)
        {
            await using var db = fixture.Db();
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261024084147_AddIntegrationOutbox");
            var first = Reservation.Book("Consultation", "clinician", Start, Start.AddMinutes(30), "", 1, null, null);
            db.Add(first);
            if (overlap) db.Add(Reservation.Book("Consultation", "clinician", Start.AddMinutes(15), Start.AddMinutes(45), "", 2, null, null));
            else first.StartUtc = Start.AddMinutes(1);
            await db.SaveChangesAsync();
            await Assert.ThrowsAnyAsync<Exception>(() => migrator.MigrateAsync());
            Assert.DoesNotContain("20261025121112_SlotOccupancy", await db.Database.GetAppliedMigrationsAsync());
            Assert.Equal(overlap ? 2 : 1, await db.Set<Reservation>().CountAsync());
        }
    }
}

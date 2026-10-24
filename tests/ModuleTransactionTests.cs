using System;
using System.Linq;
using System.Threading.Tasks;
using ClinicFlow.Ois;
using ClinicFlow.Application.Commands;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClinicFlow.Tests
{
    public sealed class ModuleTransactionTests : IAsyncLifetime
    {
        private readonly DatabaseFixture fixture = new DatabaseFixture();
        private static readonly DateTime Now = new DateTime(2030, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        private sealed class Clock : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new DateTimeOffset(Now);
        }
        public Task InitializeAsync() => fixture.InitializeAsync();
        public Task DisposeAsync() => Task.CompletedTask;

        private async Task Seed(bool released)
        {
            await using var db = fixture.Db();
            db.Add(new CareCase { Id = "case", PatientId = 1, CurrentPrescriptionId = "rx", CurrentPlanId = "plan" });
            db.Add(new Prescription { Id = "rx", CaseId = "case", TreatmentSite = "site", TreatmentMaterials = "none", TotalDoseGy = 60, FractionDoseGy = 2, Fractions = 30, SignedUtc = Now.AddDays(-1) });
            db.Add(new TreatmentPlan
            {
                Id = "plan", CaseId = "case", PrescriptionId = "rx", MachineId = "linac-a", Revision = 1,
                VerificationMethod = "method", Equipment = "equipment", MeasurementConditions = "conditions",
                ExpectedValues = "expected", StandardName = "standard", StandardVersion = "1", AcceptanceCriteria = "criteria", AllowedDeviation = "limit",
                ReviewedBy = "clinician", ReviewedUtc = Now.AddDays(-1), RegisteredUtc = Now.AddDays(-1),
                Validation = released ? "Passed" : "Pending",
            });
            db.Add(new Reservation { Id = "booking", Kind = released ? "Treatment" : "Experiment", CaseId = "case", PlanId = "plan", PatientId = released ? 1 : null, ResourceId = "linac-a", StartUtc = Now, EndUtc = Now.AddHours(1) });
            await db.SaveChangesAsync();
        }

        [Fact]
        public async Task Prescription_revision_and_treatment_start_cannot_both_succeed()
        {
            await Seed(true);
            async Task<string> Start()
            {
                await using var db = fixture.Db();
                try
                {
                    await new CommandExecutor(new EfCommandTransaction(db), new Clock()).Execute(new StartTreatmentCommand { Id = "booking", Version = 1, PlanId = "plan", IdentityChecked = true },
                        "clinician", "Clinician", "start", "race", default,
                        (session, request, actor, role, now) => session.Scheduling.StartTreatment(request, actor, role, now));
                    return "started";
                }
                catch (BusinessException error) { return error.Code; }
            }
            async Task<string> Revise()
            {
                await using var db = fixture.Db();
                try
                {
                    await new CommandExecutor(new EfCommandTransaction(db), new Clock()).Execute(new SignPrescriptionCommand { Id = "case", Version = 1, TreatmentSite = "site", TreatmentMaterials = "none", TotalDoseGy = 60, FractionDoseGy = 2, Fractions = 30, Diagnosis = "diagnosis", Text = "new rx", Reason = "revision" },
                        "clinician", "Clinician", "revise", "race", default,
                        (session, request, actor, role, now) => session.Clinical.SignPrescription(request, actor, role, now));
                    return "revised";
                }
                catch (BusinessException error) { return error.Code; }
            }
            var results = await Task.WhenAll(Start(), Revise());
            Assert.True(results.SequenceEqual(new[] { "started", "in_progress" }) || results.SequenceEqual(new[] { "not_released", "revised" }), string.Join(", ", results));
            await using var verify = fixture.Db();
            Assert.Equal(1, await verify.Set<OisAudit>().CountAsync());
            Assert.Equal(1, await verify.Idempotency.CountAsync());
            var care = await verify.Set<CareCase>().SingleAsync();
            if (results[0] == "started")
            {
                Assert.Equal("rx", care.CurrentPrescriptionId);
                Assert.Equal("plan", (await verify.Set<TreatmentDelivery>().SingleAsync()).PlanId);
            }
            else
            {
                Assert.Null(care.CurrentPlanId);
                Assert.Empty(await verify.Set<TreatmentDelivery>().ToListAsync());
            }
        }

        [Fact]
        public async Task Failure_after_validation_rolls_back_all_modules_and_allows_same_key_retry()
        {
            await Seed(false);
            var request = new SubmitExperimentResultCommand { Id = "booking", Version = 1, Outcome = "Failed", FailureType = "Equipment", MeasuredValues = "measurement", ResultAttachment = "ref", Text = "equipment failed" };
            await using (var db = fixture.Db())
            {
                await Assert.ThrowsAsync<BusinessException>(() => new CommandExecutor(new EfCommandTransaction(db), new Clock()).Execute(request,
                    "physicist", "Physicist", "validation", "rollback", default,
                    (session, input, actor, role, now) =>
                    {
                        session.Planning.SubmitExperimentResult(input, actor, role, now);
                        session.HandleDomainEvents();
                        throw new BusinessException("test_failure", "Fail after all module changes");
                    }));
            }
            await using (var db = fixture.Db())
            {
                Assert.Empty(await db.Set<PhysicalValidation>().ToListAsync());
                Assert.Equal("Pending", (await db.Set<TreatmentPlan>().SingleAsync()).Validation);
                Assert.Equal("Booked", (await db.Set<Reservation>().SingleAsync()).Status);
                Assert.True((await db.Set<OisResource>().SingleAsync(x => x.Id == "linac-a")).Available);
                Assert.Empty(await db.Set<OisTask>().ToListAsync());
                Assert.Empty(await db.Set<OisAudit>().ToListAsync());
                Assert.Empty(await db.Idempotency.ToListAsync());
                Assert.Empty(await db.Set<OutboxMessage>().ToListAsync());
            }
            await using (var db = fixture.Db())
                await new CommandExecutor(new EfCommandTransaction(db), new Clock()).Execute(request, "physicist", "Physicist", "validation", "retry", default,
                    (session, input, actor, role, now) => session.Planning.SubmitExperimentResult(input, actor, role, now));
            // A replay must not regenerate either domain effects or an integration event.
            await using (var db = fixture.Db())
                await new CommandExecutor(new EfCommandTransaction(db), new Clock()).Execute(request, "physicist", "Physicist", "validation", "replay", default,
                    (session, input, actor, role, now) => throw new Exception("Replay must not execute"));
            await using (var db = fixture.Db())
            {
                Assert.Single(await db.Set<PhysicalValidation>().ToListAsync());
                Assert.Equal("Completed", (await db.Set<Reservation>().SingleAsync()).Status);
                Assert.False((await db.Set<OisResource>().SingleAsync(x => x.Id == "linac-a")).Available);
                Assert.Single(await db.Idempotency.ToListAsync());
                var message = Assert.Single(await db.Set<OutboxMessage>().ToListAsync());
                Assert.Equal("clinicflow.plan-validated", message.Type);
                Assert.Equal(1, message.Version);
                Assert.DoesNotContain("equipment failed", message.Payload);
                Assert.Null(message.PublishedUtc);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ClinicFlow;
using ClinicFlow.Ois;
using ClinicFlow.Application.Commands;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClinicFlow.Tests
{
    public sealed class UpgradeCompatibilityTests : IAsyncLifetime
    {
        private readonly DatabaseFixture fixture = new DatabaseFixture();
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        { Converters = { new UtcDateTimeConverter() } };
        public Task InitializeAsync() => fixture.InitializeAsync();
        public Task DisposeAsync() => Task.CompletedTask;

        // This fixture is the retired wire format, independent of CommandHistory and new DTOs.
        private static string HistoricalFingerprint(string op, ScenarioCommand command)
        {
            var original = new HashSet<string> { "id", "version", "patientId", "caseId", "planId", "resourceId", "kind", "startUtc", "endUtc", "text", "diagnosis", "reason", "attachment", "outcome", "failureType", "identityChecked", "available" };
            var fields = JsonSerializer.SerializeToElement(command, Json).EnumerateObject()
                .Where(p => original.Contains(p.Name) || !(p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == "" || p.Value.ValueKind == JsonValueKind.Number && p.Value.GetDecimal() == 0))
                .ToDictionary(p => p.Name, p => p.Value);
            return Hash(JsonSerializer.Serialize(new { op, command = fields }, Json));
        }
        private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

        private async Task Replay<T>(string op, ScenarioCommand historical, T request) where T : class
        {
            string key = Guid.NewGuid().ToString();
            string id = Hash(JsonSerializer.Serialize(new[] { "ois", "historical-actor", "Clinician", key }));
            string fingerprint = HistoricalFingerprint(op, historical);
            await using (var seed = fixture.Db())
            {
                seed.Idempotency.Add(new IdempotencyRecord
                {
                    Id = id,
                    Fingerprint = fingerprint,
                    Response = "{\"targetId\":\"original-target\"}",
                });
                await seed.SaveChangesAsync();
            }
            await using (var db = fixture.Db())
            {
                var result = await new CommandExecutor(new EfCommandTransaction(db), TimeProvider.System).Execute(request,
                    "historical-actor", "Clinician", key, "upgrade-test", default,
                    (state, input, actor, role, now) => throw new Exception("Replayed request executed twice"));
                Assert.Equal("original-target", ((JsonElement)result).GetProperty("targetId").GetString());
                Assert.Equal(fingerprint, (await db.Idempotency.SingleAsync(x => x.Id == id)).Fingerprint);
                Assert.Empty(await db.Set<OisAudit>().ToListAsync());
            }
            // A changed business operation using that historical key must still conflict.
            await using (var db = fixture.Db())
            {
                var error = await Assert.ThrowsAsync<BusinessException>(() => new CommandExecutor(new EfCommandTransaction(db), TimeProvider.System).Execute(
                    new CreateCaseCommand { PatientId = 2, Text = "changed" }, "historical-actor", "Clinician", key,
                    "conflict-test", default, (state, input, actor, role, now) => state.Clinical.CreateCase(input, actor, role, now)));
                Assert.Equal("idempotency_conflict", error.Code);
            }
        }

        [Fact]
        public async Task Historical_keys_replay_through_resource_requests_without_reexecuting()
        {
            await Replay("case", new ScenarioCommand { PatientId = 1, Text = "legacy case" },
                new CreateCaseCommand { PatientId = 1, Text = "legacy case" });
            await Replay("plan", new ScenarioCommand { CaseId = "case", Version = 3, ResourceId = "linac-a", Text = "plan", VerificationMethod = "method", AllowedDeviation = "limit" },
                new RegisterPlanCommand { CaseId = "case", Version = 3, ResourceId = "linac-a", Text = "plan", VerificationMethod = "method", AllowedDeviation = "limit" });
            await Replay("book", new ScenarioCommand { Kind = "Experiment", PlanId = "plan", ResourceId = "linac-a", StartUtc = DateTimeOffset.Parse("2035-01-01T08:00:00+08:00"), EndUtc = DateTimeOffset.Parse("2035-01-01T09:00:00+08:00") },
                new BookExperimentCommand { PlanId = "plan", ResourceId = "linac-a", StartUtc = DateTimeOffset.Parse("2035-01-01T08:00:00+08:00"), EndUtc = DateTimeOffset.Parse("2035-01-01T09:00:00+08:00") });
            await Replay("save-draft", new ScenarioCommand { CaseId = "case", Version = 2, Kind = "prescribe", DraftVersion = 4, DraftJson = "{\"text\":\"历史草稿\"}" },
                new SavePrescriptionDraftCommand { CaseId = "case", Version = 2, DraftVersion = 4, DraftJson = "{\"text\":\"历史草稿\"}" });
            await Replay("correction", new ScenarioCommand { Id = "plan", Version = 2, Text = "correction", Reason = "reason" },
                new CorrectPlanCommand { Id = "plan", Version = 2, Text = "correction", Reason = "reason" });
            await Replay("machine", new ScenarioCommand { Id = "linac-a", Version = 2, Available = false, Reason = "maintenance" },
                new SetMachineAvailabilityCommand { Id = "linac-a", Version = 2, Available = false, Reason = "maintenance" });
        }

        [Fact]
        public async Task Historical_notes_corrections_and_actor_drafts_remain_readable_and_signing_consumes_draft()
        {
            await using (var seed = fixture.Db())
            {
                seed.Add(new CareCase { Id = "historical-case", PatientId = 1, Title = "Historical case" });
                seed.Add(new OisDraft { CaseId = "historical-case", CaseVersion = 1, Actor = "clinician", Operation = "prescribe", Payload = "{\"text\":\"旧草稿\"}" });
                seed.Add(new OisDraft { CaseId = "historical-case", CaseVersion = 1, Actor = "another", Operation = "prescribe", Payload = "{}" });
                foreach (string action in new[] { "note", "correction" })
                    seed.Add(new OisAudit
                    {
                        Actor = "clinician",
                        Role = "Clinician",
                        Action = action,
                        TargetId = "historical-case",
                        AtUtc = DateTime.UtcNow,
                        Detail = "{\"command\":{\"text\":\"历史内容\",\"reason\":\"历史原因\"},\"before\":[]}"
                    });
                await seed.SaveChangesAsync();
            }
            await using (var db = fixture.Db())
            {
                var snapshot = await new EfWorkspaceReader(db).Snapshot("clinician", default);
                Assert.Equal("{\"text\":\"旧草稿\"}", Assert.Single(snapshot.Drafts).Payload);
                Assert.Equal(2, snapshot.Audits.Count);
                foreach (var audit in snapshot.Audits)
                    Assert.Equal("历史内容", JsonDocument.Parse(audit.Detail).RootElement.GetProperty("command").GetProperty("text").GetString());
            }
            await using (var db = fixture.Db())
            {
                var request = new SignPrescriptionCommand { Id = "historical-case", Version = 1, Diagnosis = "diagnosis", Text = "prescription", TreatmentSite = "site", TreatmentMaterials = "none", TotalDoseGy = 60, FractionDoseGy = 2, Fractions = 30 };
                await new CommandExecutor(new EfCommandTransaction(db), TimeProvider.System).Execute(request, "clinician", "Clinician", "sign-upgrade", "test", default,
                    (state, input, actor, role, now) => state.Clinical.SignPrescription(input, actor, role, now));
            }
            await using (var db = fixture.Db())
            {
                Assert.Equal("another", (await db.Set<OisDraft>().SingleAsync()).Actor);
                Assert.Equal(2, await db.Set<OisAudit>().CountAsync(x => x.Action == "note" || x.Action == "correction"));
                Assert.Equal(1, await db.Idempotency.CountAsync());
            }
        }
    }
}

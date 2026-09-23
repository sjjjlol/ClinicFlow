using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
namespace ClinicFlow.Scheduling;
// Test probes are supplied through DI only; normal runtime never enables fault injection.
public interface ITransactionProbe { Task Reach(string point, CancellationToken ct); }
public class NoTransactionProbe : ITransactionProbe { public Task Reach(string point, CancellationToken ct) => Task.CompletedTask; }
public class SchedulingService(ClinicDb db, ITransactionProbe probe)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new UtcDateTimeConverter() } };
    static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public Task<Appointment> Create(Booking input, string actor, string key, string correlation, CancellationToken ct) =>
        Execute(actor, "create", key, input with { StartUtc = input.StartUtc.ToUniversalTime(), EndUtc = input.EndUtc.ToUniversalTime() }, async () =>
        {
            var slots = Rules.Slots(input.StartUtc.UtcDateTime, input.EndUtc.UtcDateTime);
            await LockResources([input.ResourceId], ct);
            if (!await db.Patients.AnyAsync(x => x.Id == input.PatientId, ct)) throw new BusinessException("patient_missing", "患者不存在", 404);
            await CheckFree(input.ResourceId, slots, null, ct);
            var a = new Appointment { PatientId = input.PatientId, ResourceId = input.ResourceId, StartUtc = input.StartUtc.UtcDateTime, EndUtc = input.EndUtc.UtcDateTime };
            db.Appointments.Add(a);
            Claim(a, slots);
            db.Tasks.AddRange(new PrerequisiteTask { AppointmentId = a.Id, Name = "资料核对" }, new PrerequisiteTask { AppointmentId = a.Id, Name = "预约信息复核" });
            Record(a, "Created", actor, correlation);
            await probe.Reach("before-save", ct);
            return a;
        }, ct);

    async Task<Appointment> Execute<T>(string actor, string operation, string key, T payload, Func<Task<Appointment>> action, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128) throw new BusinessException("invalid_key", "请提供1–128字符的 Idempotency-Key", 400);
        var id = Hash(JsonSerializer.Serialize(new[] { actor, operation, key }));
        var fingerprint = Hash(JsonSerializer.Serialize(payload, Json));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            // INSERT obtains the unique-key lock even for a concurrent duplicate. The row and result commit together.
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Idempotency (Id,Fingerprint,CreatedUtc) VALUES ({id},{fingerprint},{DateTime.UtcNow}) ON DUPLICATE KEY UPDATE Id=Id", ct);
            var record = (await db.Idempotency.FromSqlInterpolated($"SELECT * FROM Idempotency WHERE Id={id} FOR UPDATE").ToListAsync(ct)).Single();
            if (record.Fingerprint != fingerprint) throw new BusinessException("idempotency_conflict", "同一请求标识已用于不同内容，请重新发起操作");
            if (record.Response is not null) { await tx.CommitAsync(ct); return JsonSerializer.Deserialize<Appointment>(record.Response, Json)!; }
            var result = await action();
            record.Response = JsonSerializer.Serialize(result, Json);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException || ex is MySqlException { Number: 1213 or 1205 or 1062 } || ex.InnerException is MySqlException { Number: 1213 or 1205 or 1062 })
        {
            await tx.RollbackAsync(ct);
            throw new BusinessException("concurrent_conflict", "资源正在被其他请求修改，请刷新后重试");
        }
    }
    async Task LockResources(IEnumerable<int> ids, CancellationToken ct)
    {
        foreach (var id in ids.Distinct().Order())
            if ((await db.Resources.FromSqlInterpolated($"SELECT * FROM Resources WHERE Id={id} FOR UPDATE").ToListAsync(ct)).Count == 0)
                throw new BusinessException("resource_missing", "资源不存在", 404);
    }
    async Task CheckFree(int resource, DateTime[] slots, string? ownId, CancellationToken ct)
    {
        if (await db.SlotClaims.AnyAsync(x => x.ResourceId == resource && slots.Contains(x.SlotStartUtc) && x.AppointmentId != ownId, ct))
            throw new BusinessException("slot_conflict", "该资源时段已被占用，请选择其他时间");
    }
    void Claim(Appointment a, DateTime[] slots) => db.SlotClaims.AddRange(slots.Select(s => new SlotClaim { ResourceId = a.ResourceId, SlotStartUtc = s, AppointmentId = a.Id }));
    void Record(Appointment a, string action, string actor, string correlation)
    {
        var snapshot = JsonSerializer.Serialize(a, Json);
        db.Audits.Add(new AuditEntry { AppointmentId = a.Id, Action = action, Actor = actor, Version = a.Version, Summary = snapshot, CorrelationId = correlation });
        db.Outbox.Add(new OutboxMessage { AppointmentId = a.Id, Version = a.Version, Payload = snapshot, CorrelationId = correlation });
    }
}

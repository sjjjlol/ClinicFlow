using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace ClinicFlow.Ois;

public class OisService(ClinicDb db, TimeProvider clock)
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new UtcDateTimeConverter() } };

    public async Task<OisWorkflow> Load(CancellationToken ct) =>
        new()
        {
            Drafts = await db.Set<OisDraft>().ToListAsync(ct),
            Cases = await db.Set<CareCase>().ToListAsync(ct),
            Prescriptions = await db.Set<Prescription>().ToListAsync(ct),
            Plans = await db.Set<TreatmentPlan>().ToListAsync(ct),
            Resources = await db.Set<OisResource>().ToListAsync(ct),
            Reservations = await db.Set<Reservation>().ToListAsync(ct),
            Deliveries = await db.Set<TreatmentDelivery>().ToListAsync(ct),
            Tasks = await db.Set<OisTask>().ToListAsync(ct),
            Patients = await db.Patients.AsNoTracking().ToListAsync(ct),
        };

    public async Task<object> Snapshot(string actor, CancellationToken ct)
    {
        // Repeatable read keeps all tables and derived release status in one consistent view.
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            ct
        );
        var w = await Load(ct);
        var audits = await db.Set<OisAudit>()
            .Where(x => x.Action != "save-draft" || x.Actor == actor)
            .OrderBy(x => x.Id).ToListAsync(ct);
        var reservations = w
            .Reservations.Select(r => new
            {
                reservation = r,
                blockers = r.Status == "Booked" ? w.Blockers(r) : [],
            })
            .ToList();
        await tx.CommitAsync(ct);
        return new
        {
            drafts = w.Drafts.Where(x => x.Actor == actor),
            w.Cases,
            w.Prescriptions,
            w.Plans,
            w.Resources,
            reservations,
            w.Deliveries,
            w.Tasks,
            w.Patients,
            audits,
        };
    }

    public async Task<object> Execute(
        string op,
        OisCommand command,
        string actor,
        string role,
        string key,
        string correlation,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
            throw new BusinessException("invalid_key", "请提供1至128字符的请求标识", 400);
        if (
            typeof(OisCommand)
                .GetProperties()
                .Any(p =>
                    p.PropertyType == typeof(string)
                    && (p.GetValue(command) is not string s || s.Length > (p.Name == "DraftJson" ? 64000 : 4000))
                )
        )
            throw new BusinessException("invalid_input", "文本不能为空值或超过4000字符", 400);
        var id = Hash(JsonSerializer.Serialize(new[] { "ois", actor, role, key }));
        // Preserve fingerprints of pre-upgrade requests: absent new fields must not change replay keys.
        var legacyNames = new HashSet<string> { "id", "version", "patientId", "caseId", "planId", "resourceId", "kind", "startUtc", "endUtc", "text", "diagnosis", "reason", "attachment", "outcome", "failureType", "identityChecked", "available" };
        var fingerprintCommand = JsonSerializer.SerializeToElement(command, Json).EnumerateObject()
            .Where(p => legacyNames.Contains(p.Name) || !(p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == "" || p.Value.ValueKind == JsonValueKind.Number && p.Value.GetDecimal() == 0))
            .ToDictionary(p => p.Name, p => p.Value);
        var fingerprint = Hash(JsonSerializer.Serialize(new { op, command = fingerprintCommand }, Json));
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            ct
        );
        try
        {
            // All OIS writers acquire this row first. Serializing a small single-site demo
            // avoids phantom-overlap races between patient, machine and experiment bookings.
            await db.Set<OisGate>()
                .FromSqlRaw("SELECT * FROM OisGate WHERE Id=1 FOR UPDATE")
                .ToListAsync(ct);
            var prior = await db.Idempotency.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (prior is not null)
            {
                if (prior.Fingerprint != fingerprint)
                    throw new BusinessException("idempotency_conflict", "请求标识已用于其他内容");
                await tx.CommitAsync(ct);
                return JsonSerializer.Deserialize<JsonElement>(prior.Response!);
            }
            var w = await Load(ct);
            var now = clock.GetUtcNow().UtcDateTime;
            var before = JsonSerializer.Serialize(
                w.Cases.Cast<IOisEntity>()
                    .Concat(w.Plans)
                    .Concat(w.Reservations)
                    .Concat(w.Deliveries)
                    .Concat(w.Resources)
                    .Concat(w.Tasks)
                    .Where(x => x.Id == command.Id || x.Id == command.CaseId)
                    .Select(x => JsonSerializer.SerializeToElement(x, x.GetType(), Json)),
                Json
            );
            var targetId = w.Apply(op, command, actor, role, now);
            Track(w.Drafts);
            if (op is "prescribe" or "plan")
            {
                var caseId = op == "prescribe" ? command.Id : command.CaseId;
                db.RemoveRange(w.Drafts.Where(x => x.CaseId == caseId && x.Actor == actor && x.Operation == op));
            }
            Track(w.Cases);
            Track(w.Prescriptions);
            Track(w.Plans);
            Track(w.Resources);
            Track(w.Reservations);
            Track(w.Deliveries);
            Track(w.Tasks);
            var result = new { targetId };
            db.Set<OisAudit>()
                .Add(
                    new OisAudit
                    {
                        Actor = actor,
                        Role = role,
                        Action = op,
                        TargetId = targetId,
                        Detail = JsonSerializer.Serialize(
                            new
                            {
                                command,
                                before = JsonSerializer.Deserialize<JsonElement>(before),
                            },
                            Json
                        ),
                        CorrelationId = correlation,
                        AtUtc = now,
                    }
                );
            db.Idempotency.Add(
                new IdempotencyRecord
                {
                    Id = id,
                    Fingerprint = fingerprint,
                    Response = JsonSerializer.Serialize(result, Json),
                    CreatedUtc = now,
                }
            );
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (Exception ex)
            when (ex is DbUpdateConcurrencyException
                || ex is MySqlException { Number: 1213 or 1205 or 1062 }
                || ex.InnerException is MySqlException { Number: 1213 or 1205 or 1062 }
            )
        {
            await tx.RollbackAsync(ct);
            throw new BusinessException(
                "concurrent_conflict",
                "数据正在更新，请刷新并使用原请求标识重试"
            );
        }
    }

    void Track<T>(List<T> entities)
        where T : class
    {
        foreach (var entity in entities)
            if (db.Entry(entity).State == EntityState.Detached)
                db.Add(entity);
    }

    static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

public static class OisEndpoints
{
    public static void MapOis(this WebApplication app)
    {
        var group = app.MapGroup("/api/ois").RequireAuthorization("ois");
        group.MapGet(
            "/workspace",
            (OisService service, HttpContext ctx, CancellationToken ct) => service.Snapshot(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct)
        );
        group.MapPost(
            "/commands/{operation}",
            (
                string operation,
                OisCommand input,
                OisService service,
                HttpContext ctx,
                CancellationToken ct
            ) =>
                service.Execute(
                    operation,
                    input,
                    ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    ctx.User.FindFirstValue(ClaimTypes.Role)!,
                    ctx.Request.Headers["Idempotency-Key"].ToString(),
                    ctx.TraceIdentifier,
                    ct
                )
        );
    }
}

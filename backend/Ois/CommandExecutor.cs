using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using ClinicFlow.Ois.Compatibility;

namespace ClinicFlow.Ois
{
    public sealed class CommandExecutor
    {
        private readonly ClinicDb db;
        private readonly TimeProvider clock;

        public CommandExecutor(ClinicDb db, TimeProvider clock)
        {
            this.db = db;
            this.clock = clock;
        }

        private static readonly JsonSerializerOptions Json = CommandHistory.Json;

        public async Task<object> Execute<T>(
            T request,
            string actor,
            string role,
            string key,
            string correlation,
            CancellationToken ct,
            Func<OisSession, T, string, string, DateTime, string> apply
        ) where T : class
        {
            var op = CommandHistory.Operation<T>();
            var command = CommandHistory.Normalize(request);
            if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
                throw new BusinessException("invalid_key", "请提供1至128字符的请求标识", 400);
            foreach (var property in typeof(T).GetProperties())
            {
                if (property.PropertyType != typeof(string))
                    continue;

                string? value = property.GetValue(request) as string;
                int maxLength = property.Name == "DraftJson" ? 64000 : 4000;
                if (value == null || value.Length > maxLength)
                    throw new BusinessException("invalid_input", "文本不能为空值或超过4000字符", 400);
            }
            var id = CommandHistory.Hash(JsonSerializer.Serialize(new[] { "ois", actor, role, key }));
            var fingerprint = CommandHistory.Fingerprint(op, command);
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
                if (prior != null)
                {
                    if (prior.Fingerprint != fingerprint)
                        throw new BusinessException("idempotency_conflict", "请求标识已用于其他内容");
                    await tx.CommitAsync(ct);
                    return JsonSerializer.Deserialize<JsonElement>(prior.Response!);
                }
                var w = await OisStateStore.Load(db, ct);
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
                var targetId = apply(new OisSession(w), request, actor, role, now);
                Track(w.Drafts);
                foreach (var entry in db.ChangeTracker.Entries<OisDraft>().ToList())
                    if (!w.Drafts.Contains(entry.Entity)) db.Remove(entry.Entity);
                Track(w.Cases);
                Track(w.Prescriptions);
                Track(w.Plans);
                Track(w.Validations);
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
                when (ex is DbUpdateConcurrencyException || IsConcurrentConflict(ex) || IsConcurrentConflict(ex.InnerException))
            {
                await tx.RollbackAsync(ct);
                throw new BusinessException(
                    "concurrent_conflict",
                    "数据正在更新，请刷新并使用原请求标识重试"
                );
            }
        }

        private static bool IsConcurrentConflict(Exception? exception)
        {
            MySqlException? mysqlException = exception as MySqlException;
            return mysqlException != null
                && (mysqlException.Number == 1213 || mysqlException.Number == 1205 || mysqlException.Number == 1062);
        }

        void Track<T>(List<T> entities)
            where T : class
        {
            foreach (var entity in entities)
                if (db.Entry(entity).State == EntityState.Detached)
                    db.Add(entity);
        }

    }
}

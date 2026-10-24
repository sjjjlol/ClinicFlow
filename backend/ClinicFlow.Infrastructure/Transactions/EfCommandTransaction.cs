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
    public sealed class EfCommandTransaction : ICommandTransaction
    {
        private readonly ClinicDb db;
        public EfCommandTransaction(ClinicDb db) => this.db = db;
        private static readonly JsonSerializerOptions Json = CommandHistory.Json;
        public async Task<object> Run(CommandEnvelope command, Func<OisSession, string> apply, CancellationToken ct)
        {
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
                var prior = await db.Idempotency.SingleOrDefaultAsync(x => x.Id == command.Id, ct);
                if (prior != null)
                {
                    if (prior.Fingerprint != command.Fingerprint)
                        throw new BusinessException("idempotency_conflict", "请求标识已用于其他内容");
                    await tx.CommitAsync(ct);
                    return JsonSerializer.Deserialize<JsonElement>(prior.Response!);
                }
                var w = await OisStateStore.Load(db, ct);
                var before = JsonSerializer.Serialize(
                    w.Cases.Cast<IOisEntity>()
                        .Concat(w.Plans)
                        .Concat(w.Reservations)
                        .Concat(w.Deliveries)
                        .Concat(w.Resources)
                        .Concat(w.Tasks)
                        .Where(x => x.Id == command.TargetId || x.Id == command.CaseId)
                        .Select(x => JsonSerializer.SerializeToElement(x, x.GetType(), Json)),
                    Json
                );
                var session = new OisSession(w);
                var targetId = apply(session);
                var now = command.OccurredUtc;
                foreach (var message in session.IntegrationEvents)
                    db.Set<OutboxMessage>().Add(OutboxMessage.From(message));
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
                            Actor = command.Actor,
                            Role = command.Role,
                            Action = command.Operation,
                            TargetId = targetId,
                            Detail = JsonSerializer.Serialize(
                                new
                                {
                                    command = JsonSerializer.Deserialize<JsonElement>(command.CommandJson),
                                    before = JsonSerializer.Deserialize<JsonElement>(before),
                                },
                                Json
                            ),
                            CorrelationId = command.Correlation,
                            AtUtc = now,
                        }
                    );
                db.Idempotency.Add(
                    new IdempotencyRecord
                    {
                        Id = command.Id,
                        Fingerprint = command.Fingerprint,
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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Data;
using Microsoft.EntityFrameworkCore;
using ClinicFlow.Application.Integration;

namespace ClinicFlow.Ois
{
    public sealed class OutboxDispatcher
    {
        private readonly ClinicDb db;
        private readonly IIntegrationEventPublisher publisher;
        private readonly TimeProvider clock;
        public OutboxDispatcher(ClinicDb db, IIntegrationEventPublisher publisher, TimeProvider clock)
        { this.db = db; this.publisher = publisher; this.clock = clock; }
        public async Task<int> DispatchBatch(int batchSize, CancellationToken ct)
        {
            if (batchSize < 1 || batchSize > 100) throw new ArgumentOutOfRangeException(nameof(batchSize));
            var now = clock.GetUtcNow().UtcDateTime;
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            // Separate from OisGate. Locked rows are skipped by concurrent delivery workers.
            var messages = await db.Set<OutboxMessage>().FromSqlInterpolated($@"
                SELECT * FROM OutboxMessage
                WHERE PublishedUtc IS NULL AND NextAttemptUtc <= {now}
                ORDER BY OccurredUtc, Id LIMIT {batchSize} FOR UPDATE SKIP LOCKED").ToListAsync(ct);
            var published = 0;
            foreach (var message in messages)
            {
                message.Attempts++;
                try
                {
                    await publisher.Publish(message.ToEvent(), ct);
                    message.PublishedUtc = clock.GetUtcNow().UtcDateTime;
                    message.LastError = null;
                    published++;
                }
                catch (Exception error) when (!(error is OperationCanceledException))
                {
                    // Do not persist exception messages: transports may include credentials or payloads.
                    message.LastError = error.GetType().Name;
                    message.NextAttemptUtc = now.AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(message.Attempts, 8))));
                }
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return published;
        }
    }

}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClinicFlow.Application.Integration
{
    public sealed class IntegrationEvent
    {
        public string EventId { get; }
        public string Type { get; }
        public int Version { get; }
        public DateTime OccurredUtc { get; }
        public string Payload { get; }
        public IntegrationEvent(string eventId, string type, int version, DateTime occurredUtc, string payload)
        { EventId = eventId; Type = type; Version = version; OccurredUtc = occurredUtc; Payload = payload; }
    }
    public interface IIntegrationEventPublisher
    {
        // At-least-once delivery: consumers must deduplicate by EventId.
        Task Publish(IntegrationEvent message, CancellationToken ct);
    }

}

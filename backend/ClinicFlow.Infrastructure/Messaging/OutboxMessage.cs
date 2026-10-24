using System;
using System.Collections.Generic;
using ClinicFlow.Application.Integration;

namespace ClinicFlow.Ois
{
    public sealed class OutboxMessage
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public int Version { get; set; }
        public string Payload { get; set; } = "";
        public DateTime OccurredUtc { get; set; }
        public DateTime? PublishedUtc { get; set; }
        public int Attempts { get; set; }
        public DateTime NextAttemptUtc { get; set; }
        public string? LastError { get; set; }
        internal static OutboxMessage From(IntegrationEvent message) => new OutboxMessage
        { Id = message.EventId, Type = message.Type, Version = message.Version, Payload = message.Payload,
            OccurredUtc = message.OccurredUtc, NextAttemptUtc = message.OccurredUtc };
        internal IntegrationEvent ToEvent() => new IntegrationEvent(Id, Type, Version, OccurredUtc, Payload);
    }

}

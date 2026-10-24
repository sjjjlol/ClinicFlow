using System;
using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class RescheduleReservationRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string ResourceId { get; set; } = "";
        public DateTimeOffset? StartUtc { get; set; }
        public DateTimeOffset? EndUtc { get; set; }
        public string Reason { get; set; } = "";
        public ClinicFlow.Application.Commands.RescheduleReservationCommand ToCommand() =>
            new ClinicFlow.Application.Commands.RescheduleReservationCommand
            {
                Id = Id,
                Version = Version,
                ResourceId = ResourceId,
                StartUtc = StartUtc,
                EndUtc = EndUtc,
                Reason = Reason,
            };
    }
}

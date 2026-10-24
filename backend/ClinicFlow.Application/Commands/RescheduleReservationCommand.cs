using System;

namespace ClinicFlow.Application.Commands
{
    [ClinicFlow.Ois.BusinessOperation("reschedule")]
    public sealed class RescheduleReservationCommand
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string ResourceId { get; set; } = "";
        public DateTimeOffset? StartUtc { get; set; }
        public DateTimeOffset? EndUtc { get; set; }
        public string Reason { get; set; } = "";
    }
}

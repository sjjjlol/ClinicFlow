using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicFlow.Ois
{
    public sealed class TreatmentDeliveryResponse
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string ReservationId { get; set; } = "";
        public string PlanId { get; set; } = "";
        public string MachineId { get; set; } = "";
        public string StartedBy { get; set; } = "";
        public DateTime StartedUtc { get; set; }
        public string Outcome { get; set; } = "";
        public string? FinishedBy { get; set; }
        public DateTime? FinishedUtc { get; set; }
        public string Note { get; set; } = "";
    }
}

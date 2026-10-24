using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicFlow.Ois
{
    public sealed class CareCaseResponse
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public int PatientId { get; set; }
        public string Title { get; set; } = "";
        public bool Terminated { get; set; }
        public string TerminationReason { get; set; } = "";
        public string? CurrentPrescriptionId { get; set; }
        public string? CurrentPlanId { get; set; }
    }
}

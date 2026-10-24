using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicFlow.Ois
{
    public sealed class ReservationResponse
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Kind { get; set; } = "";
        public int? PatientId { get; set; }
        public string? CaseId { get; set; }
        public string? PlanId { get; set; }
        public string ResourceId { get; set; } = "";
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public string Status { get; set; } = "";
        public string Reason { get; set; } = "";
        public string MeasuredValues { get; set; } = "";
        public string ResultAttachment { get; set; } = "";
        public string ValidationOutcome { get; set; } = "";
        public string ValidationNote { get; set; } = "";
        public string FailureType { get; set; } = "";
        public string SignedBy { get; set; } = "";
        public DateTime? SignedUtc { get; set; }
    }
}

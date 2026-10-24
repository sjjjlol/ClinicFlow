using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicFlow.Ois
{
    public sealed class TreatmentPlanResponse
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string CaseId { get; set; } = "";
        public string PrescriptionId { get; set; } = "";
        public int Revision { get; set; }
        public string VerificationMethod { get; set; } = "";
        public string Equipment { get; set; } = "";
        public string MeasurementConditions { get; set; } = "";
        public string ExpectedValues { get; set; } = "";
        public string StandardName { get; set; } = "";
        public string StandardVersion { get; set; } = "";
        public string AcceptanceCriteria { get; set; } = "";
        public string AllowedDeviation { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Attachment { get; set; } = "";
        public string MachineId { get; set; } = "";
        public string RegisteredBy { get; set; } = "";
        public DateTime RegisteredUtc { get; set; }
        public string? ReviewedBy { get; set; }
        public DateTime? ReviewedUtc { get; set; }
        public string Validation { get; set; } = "";
        public string? ValidatedBy { get; set; }
        public DateTime? ValidatedUtc { get; set; }
        public string ValidationNote { get; set; } = "";
        public string FailureType { get; set; } = "";
    }
}

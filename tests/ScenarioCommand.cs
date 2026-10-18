using System;
namespace ClinicFlow.Tests
{
    public class ScenarioCommand
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public int PatientId { get; set; }
        public string CaseId { get; set; } = "";
        public string PlanId { get; set; } = "";
        public string ResourceId { get; set; } = "";
        public string Kind { get; set; } = "";
        public DateTimeOffset? StartUtc { get; set; }
        public DateTimeOffset? EndUtc { get; set; }
        public string Text { get; set; } = "";
        public string Diagnosis { get; set; } = "";
        public string Reason { get; set; } = "";
        public string Attachment { get; set; } = "";
        public string Outcome { get; set; } = "";
        public string FailureType { get; set; } = "";
        public bool IdentityChecked { get; set; }
        public string TreatmentSite { get; set; } = "";
        public decimal TotalDoseGy { get; set; }
        public decimal FractionDoseGy { get; set; }
        public int Fractions { get; set; }
        public string TreatmentMaterials { get; set; } = "";
        public string VerificationMethod { get; set; } = "";
        public string Equipment { get; set; } = "";
        public string MeasurementConditions { get; set; } = "";
        public string ExpectedValues { get; set; } = "";
        public string StandardName { get; set; } = "";
        public string StandardVersion { get; set; } = "";
        public string AcceptanceCriteria { get; set; } = "";
        public string AllowedDeviation { get; set; } = "";
        public string MeasuredValues { get; set; } = "";
        public string ResultAttachment { get; set; } = "";
        public string DraftJson { get; set; } = "";
        public int DraftVersion { get; set; }
        public bool Available { get; set; } = true;
    }
}

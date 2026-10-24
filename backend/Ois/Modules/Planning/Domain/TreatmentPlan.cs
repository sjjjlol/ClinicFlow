using System;
using System.Linq;
using static ClinicFlow.Ois.DomainRules;

namespace ClinicFlow.Ois
{
    public class TreatmentPlan : IOisEntity
    {
        public string Id { get; internal set; } = Guid.NewGuid().ToString();
        public int Version { get; internal set; } = 1;
        public string CaseId { get; internal set; } = "";
        public string PrescriptionId { get; internal set; } = "";
        public int Revision { get; internal set; }
        public string VerificationMethod { get; internal set; } = "";
        public string Equipment { get; internal set; } = "";
        public string MeasurementConditions { get; internal set; } = "";
        public string ExpectedValues { get; internal set; } = "";
        public string StandardName { get; internal set; } = "";
        public string StandardVersion { get; internal set; } = "";
        public string AcceptanceCriteria { get; internal set; } = "";
        public string AllowedDeviation { get; internal set; } = "";
        public string Summary { get; internal set; } = "";
        public string Attachment { get; internal set; } = "";
        public string MachineId { get; internal set; } = "";
        public string RegisteredBy { get; internal set; } = "";
        public DateTime RegisteredUtc { get; internal set; }
        public string? ReviewedBy { get; internal set; }
        public DateTime? ReviewedUtc { get; internal set; }
        public string Validation { get; internal set; } = "Pending";
        public string? ValidatedBy { get; internal set; }
        public DateTime? ValidatedUtc { get; internal set; }
        public string ValidationNote { get; internal set; } = "";
        public string FailureType { get; internal set; } = "";
        internal bool HasVerificationDetails() => new[]
        {
            VerificationMethod, Equipment, MeasurementConditions, ExpectedValues,
            StandardName, StandardVersion, AcceptanceCriteria, AllowedDeviation,
        }.All(x => !string.IsNullOrWhiteSpace(x));
        internal bool CanExperiment() => Validation == "Pending" || (Validation == "Failed" && FailureType != "Clinical");
        internal void Review(string actor, DateTime now)
        {
            Need(ReviewedBy == null, "signed", "本版本已审核；修改请创建新版本");
            Need(Validation != "Failed", "failed_plan", "验证失败的计划须修订为新版本");
            ReviewedBy = actor;
            ReviewedUtc = now;
            Version++;
        }
        internal void RecordValidation(PhysicalValidation result)
        {
            Need(CanExperiment(), "signed", "本版本不可验证");
            Need(ReviewedBy != null, "review_required", "计划尚未审核");
            Validation = result.ValidationOutcome;
            ValidatedBy = result.SignedBy;
            ValidatedUtc = result.SignedUtc;
            ValidationNote = result.ValidationNote;
            FailureType = result.FailureType;
            Version++;
        }
    }
}

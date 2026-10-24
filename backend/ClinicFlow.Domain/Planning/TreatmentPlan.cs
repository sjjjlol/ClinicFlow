using System;
using System.Collections.Generic;
using ClinicFlow.Ois.Events;
using System.Linq;
using static ClinicFlow.Ois.DomainRules;

namespace ClinicFlow.Ois
{
    public class TreatmentPlan : IOisEntity, IHasDomainEvents
    {
        private readonly List<DomainEvent> events = new List<DomainEvent>();
        public IReadOnlyList<DomainEvent> DequeueDomainEvents()
        {
            var pending = events.ToArray();
            events.Clear();
            return pending;
        }
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
        public bool HasVerificationDetails() => new[]
        {
            VerificationMethod, Equipment, MeasurementConditions, ExpectedValues,
            StandardName, StandardVersion, AcceptanceCriteria, AllowedDeviation,
        }.All(x => !string.IsNullOrWhiteSpace(x));
        public bool CanExperiment() => Validation == "Pending" || (Validation == "Failed" && FailureType != "Clinical");
        public void Review(string actor, DateTime now)
        {
            Need(ReviewedBy == null, "signed", "本版本已审核；修改请创建新版本");
            Need(Validation != "Failed", "failed_plan", "验证失败的计划须修订为新版本");
            ReviewedBy = actor;
            ReviewedUtc = now;
            Version++;
        }
        public void RecordValidation(PhysicalValidation result)
        {
            Need(CanExperiment(), "signed", "本版本不可验证");
            Need(ReviewedBy != null, "review_required", "计划尚未审核");
            events.Add(new PlanValidated(Id, CaseId, result.ReservationId, MachineId, result.ValidationOutcome, result.FailureType, result.ValidationNote, result.SignedBy, result.SignedUtc!.Value));
            Validation = result.ValidationOutcome;
            ValidatedBy = result.SignedBy;
            ValidatedUtc = result.SignedUtc;
            ValidationNote = result.ValidationNote;
            FailureType = result.FailureType;
            Version++;
        }
        public static TreatmentPlan Register(string caseId, string prescriptionId, int revision, VerificationProtocol protocol,
            string summary, string attachment, string machineId, string actor, DateTime now)
        {
            Need(attachment.Length <= 1000, "invalid_text", "附件引用过长", BusinessErrorKind.InvalidInput);
            var plan = new TreatmentPlan
            {
                CaseId = caseId, PrescriptionId = prescriptionId, Revision = revision,
                VerificationMethod = protocol.Method, Equipment = protocol.Equipment, MeasurementConditions = protocol.Conditions,
                ExpectedValues = protocol.Expected, StandardName = protocol.Standard, StandardVersion = protocol.StandardVersion,
                AcceptanceCriteria = protocol.Criteria, AllowedDeviation = protocol.Deviation,
                Summary = Text(summary), Attachment = attachment, MachineId = machineId, RegisteredBy = actor, RegisteredUtc = now,
            };
            plan.events.Add(new PlanRegistered(plan.Id, caseId, prescriptionId, actor, now));
            return plan;
        }
    }
}

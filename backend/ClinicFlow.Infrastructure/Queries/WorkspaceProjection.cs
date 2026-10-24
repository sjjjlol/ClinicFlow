using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicFlow.Ois
{
    internal static class WorkspaceProjection
    {
        internal static OisDraftResponse Project(OisDraft value) => new OisDraftResponse
        {
            Id = value.Id,
            Version = value.Version,
            CaseId = value.CaseId,
            Actor = value.Actor,
            Operation = value.Operation,
            Payload = value.Payload,
            CaseVersion = value.CaseVersion,
        };
        internal static CareCaseResponse Project(CareCase value) => new CareCaseResponse
        {
            Id = value.Id,
            Version = value.Version,
            PatientId = value.PatientId,
            Title = value.Title,
            Terminated = value.Terminated,
            TerminationReason = value.TerminationReason,
            CurrentPrescriptionId = value.CurrentPrescriptionId,
            CurrentPlanId = value.CurrentPlanId,
        };
        internal static PrescriptionResponse Project(Prescription value) => new PrescriptionResponse
        {
            Id = value.Id,
            Version = value.Version,
            CaseId = value.CaseId,
            SupersedesId = value.SupersedesId,
            TreatmentSite = value.TreatmentSite,
            TotalDoseGy = value.TotalDoseGy,
            FractionDoseGy = value.FractionDoseGy,
            Fractions = value.Fractions,
            TreatmentMaterials = value.TreatmentMaterials,
            Diagnosis = value.Diagnosis,
            Content = value.Content,
            Reason = value.Reason,
            SignedBy = value.SignedBy,
            SignedUtc = value.SignedUtc,
        };
        internal static TreatmentPlanResponse Project(TreatmentPlan value) => new TreatmentPlanResponse
        {
            Id = value.Id,
            Version = value.Version,
            CaseId = value.CaseId,
            PrescriptionId = value.PrescriptionId,
            Revision = value.Revision,
            VerificationMethod = value.VerificationMethod,
            Equipment = value.Equipment,
            MeasurementConditions = value.MeasurementConditions,
            ExpectedValues = value.ExpectedValues,
            StandardName = value.StandardName,
            StandardVersion = value.StandardVersion,
            AcceptanceCriteria = value.AcceptanceCriteria,
            AllowedDeviation = value.AllowedDeviation,
            Summary = value.Summary,
            Attachment = value.Attachment,
            MachineId = value.MachineId,
            RegisteredBy = value.RegisteredBy,
            RegisteredUtc = value.RegisteredUtc,
            ReviewedBy = value.ReviewedBy,
            ReviewedUtc = value.ReviewedUtc,
            Validation = value.Validation,
            ValidatedBy = value.ValidatedBy,
            ValidatedUtc = value.ValidatedUtc,
            ValidationNote = value.ValidationNote,
            FailureType = value.FailureType,
        };
        internal static OisResourceResponse Project(OisResource value) => new OisResourceResponse
        {
            Id = value.Id,
            Version = value.Version,
            Name = value.Name,
            Kind = value.Kind,
            Available = value.Available,
            Reason = value.Reason,
        };
        internal static TreatmentDeliveryResponse Project(TreatmentDelivery value) => new TreatmentDeliveryResponse
        {
            Id = value.Id,
            Version = value.Version,
            ReservationId = value.ReservationId,
            PlanId = value.PlanId,
            MachineId = value.MachineId,
            StartedBy = value.StartedBy,
            StartedUtc = value.StartedUtc,
            Outcome = value.Outcome,
            FinishedBy = value.FinishedBy,
            FinishedUtc = value.FinishedUtc,
            Note = value.Note,
        };
        internal static OisTaskResponse Project(OisTask value) => new OisTaskResponse
        {
            Id = value.Id,
            Version = value.Version,
            TargetId = value.TargetId,
            Role = value.Role,
            Reason = value.Reason,
            ReadBy = value.ReadBy,
            ResolvedBy = value.ResolvedBy,
            Resolution = value.Resolution,
            CreatedUtc = value.CreatedUtc,
            ResolvedUtc = value.ResolvedUtc,
        };
        internal static PatientResponse Project(Patient value) => new PatientResponse
        {
            Id = value.Id,
            Name = value.Name,
            Identifier = value.Identifier,
        };
        internal static OisAuditResponse Project(OisAudit value) => new OisAuditResponse
        {
            Id = value.Id,
            Actor = value.Actor,
            Role = value.Role,
            Action = value.Action,
            TargetId = value.TargetId,
            Detail = value.Detail,
            CorrelationId = value.CorrelationId,
            AtUtc = value.AtUtc,
        };
    }
}

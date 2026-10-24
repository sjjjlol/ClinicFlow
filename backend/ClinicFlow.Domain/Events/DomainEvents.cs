using System;
using System.Collections.Generic;

namespace ClinicFlow.Ois.Events
{
    public interface IHasDomainEvents
    {
        IReadOnlyList<DomainEvent> DequeueDomainEvents();
    }
    public abstract class DomainEvent
    {
        public string EventId { get; } = Guid.NewGuid().ToString();
        public DateTime OccurredUtc { get; }
        protected DomainEvent(DateTime occurredUtc) => OccurredUtc = occurredUtc;
    }
    public sealed class PrescriptionSigned : DomainEvent
    {
        public string CaseId { get; }
        public string PrescriptionId { get; }
        public string Actor { get; }
        public PrescriptionSigned(string caseId, string prescriptionId, string actor, DateTime occurredUtc) : base(occurredUtc)
        {
            CaseId = caseId;
            PrescriptionId = prescriptionId;
            Actor = actor;
        }
    }
    public sealed class CaseTerminated : DomainEvent
    {
        public string CaseId { get; }
        public string Reason { get; }
        public string Actor { get; }
        public CaseTerminated(string caseId, string reason, string actor, DateTime occurredUtc) : base(occurredUtc)
        {
            CaseId = caseId;
            Reason = reason;
            Actor = actor;
        }
    }
    public sealed class PlanRegistered : DomainEvent
    {
        public string PlanId { get; }
        public string CaseId { get; }
        public string PrescriptionId { get; }
        public string Actor { get; }
        public PlanRegistered(string planId, string caseId, string prescriptionId, string actor, DateTime occurredUtc) : base(occurredUtc)
        {
            PlanId = planId;
            CaseId = caseId;
            PrescriptionId = prescriptionId;
            Actor = actor;
        }
    }
    public sealed class PlanValidated : DomainEvent
    {
        public string PlanId { get; }
        public string CaseId { get; }
        public string ReservationId { get; }
        public string MachineId { get; }
        public string Outcome { get; }
        public string FailureType { get; }
        public string Note { get; }
        public string Actor { get; }
        public PlanValidated(string planId, string caseId, string reservationId, string machineId, string outcome, string failureType, string note, string actor, DateTime occurredUtc) : base(occurredUtc)
        {
            PlanId = planId;
            CaseId = caseId;
            ReservationId = reservationId;
            MachineId = machineId;
            Outcome = outcome;
            FailureType = failureType;
            Note = note;
            Actor = actor;
        }
    }
    public sealed class TreatmentFinished : DomainEvent
    {
        public string DeliveryId { get; }
        public string ReservationId { get; }
        public string PlanId { get; }
        public string Outcome { get; }
        public string Note { get; }
        public string Actor { get; }
        public TreatmentFinished(string deliveryId, string reservationId, string planId, string outcome, string note, string actor, DateTime occurredUtc) : base(occurredUtc)
        {
            DeliveryId = deliveryId;
            ReservationId = reservationId;
            PlanId = planId;
            Outcome = outcome;
            Note = note;
            Actor = actor;
        }
    }

}

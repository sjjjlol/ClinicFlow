using System;
using System.Collections.Generic;

namespace ClinicFlow.Application.Integration
{
    public sealed class PrescriptionSignedV1
    {
        public string CaseId { get; set; } = "";
        public string PrescriptionId { get; set; } = "";
    }
    public sealed class CaseTerminatedV1
    {
        public string CaseId { get; set; } = "";
    }
    public sealed class PlanRegisteredV1
    {
        public string PlanId { get; set; } = "";
        public string CaseId { get; set; } = "";
        public string PrescriptionId { get; set; } = "";
    }
    public sealed class PlanValidatedV1
    {
        public string PlanId { get; set; } = "";
        public string CaseId { get; set; } = "";
        public string ReservationId { get; set; } = "";
        public string MachineId { get; set; } = "";
        public string Outcome { get; set; } = "";
        public string FailureType { get; set; } = "";
    }
    public sealed class TreatmentFinishedV1
    {
        public string DeliveryId { get; set; } = "";
        public string ReservationId { get; set; } = "";
        public string PlanId { get; set; } = "";
        public string Outcome { get; set; } = "";
    }

}

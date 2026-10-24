using System.Collections.Generic;

namespace ClinicFlow.Ois
{
    public sealed class WorkspaceResponse
    {
        public List<OisDraftResponse> Drafts { get; set; } = new List<OisDraftResponse>();
        public List<CareCaseResponse> Cases { get; set; } = new List<CareCaseResponse>();
        public List<PrescriptionResponse> Prescriptions { get; set; } = new List<PrescriptionResponse>();
        public List<TreatmentPlanResponse> Plans { get; set; } = new List<TreatmentPlanResponse>();
        public List<OisResourceResponse> Resources { get; set; } = new List<OisResourceResponse>();
        public List<WorkspaceReservation> Reservations { get; set; } = new List<WorkspaceReservation>();
        public List<TreatmentDeliveryResponse> Deliveries { get; set; } = new List<TreatmentDeliveryResponse>();
        public List<OisTaskResponse> Tasks { get; set; } = new List<OisTaskResponse>();
        public List<PatientResponse> Patients { get; set; } = new List<PatientResponse>();
        public List<OisAuditResponse> Audits { get; set; } = new List<OisAuditResponse>();
    }
    public sealed class WorkspaceReservation
    {
        public ReservationResponse Reservation { get; set; } = null!;
        public List<string> Blockers { get; set; } = new List<string>();
    }
}

using System.Collections.Generic;

namespace ClinicFlow.Ois
{
    public sealed class WorkspaceResponse
    {
        public List<OisDraft> Drafts { get; set; } = new List<OisDraft>();
        public List<CareCase> Cases { get; set; } = new List<CareCase>();
        public List<Prescription> Prescriptions { get; set; } = new List<Prescription>();
        public List<TreatmentPlan> Plans { get; set; } = new List<TreatmentPlan>();
        public List<OisResource> Resources { get; set; } = new List<OisResource>();
        public List<WorkspaceReservation> Reservations { get; set; } = new List<WorkspaceReservation>();
        public List<TreatmentDelivery> Deliveries { get; set; } = new List<TreatmentDelivery>();
        public List<OisTask> Tasks { get; set; } = new List<OisTask>();
        public List<Patient> Patients { get; set; } = new List<Patient>();
        public List<OisAudit> Audits { get; set; } = new List<OisAudit>();
    }
    public sealed class WorkspaceReservation
    {
        public ReservationResponse Reservation { get; set; } = null!;
        public List<string> Blockers { get; set; } = new List<string>();
    }
}

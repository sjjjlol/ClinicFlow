using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicFlow.Ois
{
    // Infrastructure-only unit-of-work data. Modules receive only their owned collections.
    internal sealed class OisState
    {
        public List<OisDraft> Drafts { get; set; } = new List<OisDraft>();
        public List<CareCase> Cases { get; set; } = new List<CareCase>();
        public List<Prescription> Prescriptions { get; set; } = new List<Prescription>();
        public List<TreatmentPlan> Plans { get; set; } = new List<TreatmentPlan>();
        public List<OisResource> Resources { get; set; } = new List<OisResource>();
        public List<Reservation> Reservations { get; set; } = new List<Reservation>();
        public List<TreatmentDelivery> Deliveries { get; set; } = new List<TreatmentDelivery>();
        public List<OisTask> Tasks { get; set; } = new List<OisTask>();
        public List<PhysicalValidation> Validations { get; set; } = new List<PhysicalValidation>();
        public List<Patient> Patients { get; set; } = new List<Patient>();

    }
}

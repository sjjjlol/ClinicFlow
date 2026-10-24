using System;
using System.Collections.Generic;
using System.Linq;
using ClinicFlow.Ois.Contracts;
using ClinicFlow.Application.Commands;
using static ClinicFlow.Ois.DomainRules;
using ClinicFlow.Ois.Modules.Support;

namespace ClinicFlow.Ois.Modules.Clinical
{
    public sealed partial class ClinicalModule : IClinicalContext
    {
        private readonly List<CareCase> cases;
        private readonly List<Prescription> prescriptions;
        private readonly List<Patient> patients;
        private readonly Func<ISchedulingContext> scheduling;
        private readonly Func<SupportModule> support;
        internal ClinicalModule(List<CareCase> cases, List<Prescription> prescriptions, List<Patient> patients,
            Func<ISchedulingContext> scheduling, Func<SupportModule> support)
        { this.cases = cases; this.prescriptions = prescriptions; this.patients = patients; this.scheduling = scheduling; this.support = support; }
        public CaseSummary Case(string id, int? version = null)
        {
            var care = version.HasValue ? Current(cases, id, version.Value) : Get(cases, id);
            return new CaseSummary(care.Id, care.PatientId, care.Terminated, care.CurrentPrescriptionId, care.CurrentPlanId);
        }
        public bool PatientExists(int id) => patients.Any(x => x.Id == id);
        public bool HasClinical(string prescriptionId) => Get(prescriptions, prescriptionId).HasClinicalDetails();
        public void SelectPlan(string caseId, string? planId) => Get(cases, caseId).SelectPlan(planId);
    }
}

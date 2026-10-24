using System;
using static ClinicFlow.Ois.DomainRules;

namespace ClinicFlow.Ois
{
    public class CareCase : IOisEntity
    {
        public string Id { get; internal set; } = Guid.NewGuid().ToString();
        public int Version { get; internal set; } = 1;
        public int PatientId { get; internal set; }
        public string Title { get; internal set; } = "";
        public bool Terminated { get; internal set; }
        public string TerminationReason { get; internal set; } = "";
        public string? CurrentPrescriptionId { get; internal set; }
        public string? CurrentPlanId { get; internal set; }
        internal void SignPrescription(string prescriptionId)
        {
            Need(!Terminated, "terminated", "诊疗档案已终止");
            CurrentPrescriptionId = prescriptionId;
            CurrentPlanId = null;
            Version++;
        }
        internal void SelectPlan(string? planId)
        {
            Need(planId == null || !Terminated, "terminated", "诊疗档案已终止");
            CurrentPlanId = planId;
            Version++;
        }
        internal void Terminate(string reason)
        {
            Need(!Terminated, "terminated", "诊疗档案已终止");
            TerminationReason = Text(reason);
            Terminated = true;
            Version++;
        }
    }

}

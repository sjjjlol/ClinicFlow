using System;
using System.Collections.Generic;
using ClinicFlow.Ois.Events;
using static ClinicFlow.Ois.DomainRules;

namespace ClinicFlow.Ois
{
    public class CareCase : IOisEntity, IHasDomainEvents
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
        public int PatientId { get; internal set; }
        public string Title { get; internal set; } = "";
        public bool Terminated { get; internal set; }
        public string TerminationReason { get; internal set; } = "";
        public string? CurrentPrescriptionId { get; internal set; }
        public string? CurrentPlanId { get; internal set; }
        public void SignPrescription(string prescriptionId, string actor, DateTime now)
        {
            Need(!Terminated, "terminated", "诊疗档案已终止");
            events.Add(new PrescriptionSigned(Id, prescriptionId, actor, now));
            CurrentPrescriptionId = prescriptionId;
            CurrentPlanId = null;
            Version++;
        }
        public void SelectPlan(string? planId)
        {
            Need(planId == null || !Terminated, "terminated", "诊疗档案已终止");
            CurrentPlanId = planId;
            Version++;
        }
        public void Terminate(string reason, string actor, DateTime now)
        {
            Need(!Terminated, "terminated", "诊疗档案已终止");
            TerminationReason = Text(reason);
            Terminated = true;
            events.Add(new CaseTerminated(Id, reason, actor, now));
            Version++;
        }
        public static CareCase Open(int patientId, string title) => new CareCase { PatientId = patientId, Title = Text(title) };
    }

}

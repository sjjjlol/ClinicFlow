using System;
using static ClinicFlow.Ois.DomainRules;

namespace ClinicFlow.Ois
{
    public class Reservation : IOisEntity
    {
        public string Id { get; internal set; } = Guid.NewGuid().ToString();
        public int Version { get; internal set; } = 1;
        public string Kind { get; internal set; } = "Consultation";
        public int? PatientId { get; internal set; }
        public string? CaseId { get; internal set; }
        public string? PlanId { get; internal set; }
        public string ResourceId { get; internal set; } = "";
        public DateTime StartUtc { get; internal set; }
        public DateTime EndUtc { get; internal set; }
        public string Status { get; internal set; } = "Booked";
        public string Reason { get; internal set; } = "";
        public void CompleteExperiment()
        {
            Need(Kind == "Experiment" && Status == "Booked", "invalid_state", "实验不可提交结果");
            Status = "Completed";
            Version++;
        }
        public void StartTreatment()
        {
            Need(Kind == "Treatment" && Status == "Booked", "invalid_state", "预约不可开始治疗");
            Status = "InProgress";
            Version++;
        }
        public void FinishTreatment()
        {
            Need(Kind == "Treatment" && Status == "InProgress", "invalid_state", "治疗尚未开始或已结束");
            Status = "Completed";
            Version++;
        }
        public void Cancel(string reason)
        {
            Need(Status == "Booked", "invalid_state", "只可处理尚未执行的预约");
            Reason = Text(reason);
            Status = "Cancelled";
            Version++;
        }
        public static Reservation Book(string kind, string resourceId, DateTime start, DateTime end,
            string reason, int? patientId, string? caseId, string? planId) => new Reservation
        {
            Kind = kind, ResourceId = resourceId, StartUtc = start, EndUtc = end, Reason = reason,
            PatientId = patientId, CaseId = caseId, PlanId = planId,
        };
        public Reservation ProposeSchedule(string resourceId, DateTime start, DateTime end) => new Reservation
        { Id = Id, Kind = Kind, PatientId = PatientId, CaseId = CaseId, PlanId = PlanId, ResourceId = resourceId, StartUtc = start, EndUtc = end };
        public void ApplySchedule(Reservation proposal, string reason)
        {
            Need(Status == "Booked" || Status == "Cancelled", "invalid_state", "当前状态不可改期或恢复");
            var text = Text(reason);
            ResourceId = proposal.ResourceId; StartUtc = proposal.StartUtc; EndUtc = proposal.EndUtc;
            Status = "Booked"; Reason = text; Version++;
        }
        public void RecordNoShow(string reason, DateTime now)
        {
            Need(Status == "Booked", "invalid_state", "只可处理尚未执行的预约");
            Need(PatientId != null && now >= EndUtc, "invalid_state", "预约结束后才可登记未到场");
            Reason = Text(reason); Status = "NoShow"; Version++;
        }
        public void RecordNotExecuted(string reason, DateTime now)
        {
            Need(Kind == "Treatment" && Status == "Booked" && now >= StartUtc, "invalid_state", "该预约不可登记未执行");
            Reason = Text(reason); Status = "NotExecuted"; Version++;
        }
        public void CompleteConsultation(string caseId, DateTime now)
        {
            Need(Kind == "Consultation" && Status == "Booked" && now >= StartUtc, "invalid_state", "门诊尚未开始或已处理");
            CaseId = caseId; Status = "Completed"; Version++;
        }
    }
}

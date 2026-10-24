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
        internal void CompleteExperiment()
        {
            Need(Kind == "Experiment" && Status == "Booked", "invalid_state", "实验不可提交结果");
            Status = "Completed";
            Version++;
        }
        internal void StartTreatment()
        {
            Need(Kind == "Treatment" && Status == "Booked", "invalid_state", "预约不可开始治疗");
            Status = "InProgress";
            Version++;
        }
        internal void FinishTreatment()
        {
            Need(Kind == "Treatment" && Status == "InProgress", "invalid_state", "治疗尚未开始或已结束");
            Status = "Completed";
            Version++;
        }
        internal void Cancel(string reason)
        {
            Need(Status == "Booked", "invalid_state", "只可处理尚未执行的预约");
            Reason = Text(reason);
            Status = "Cancelled";
            Version++;
        }
    }
}

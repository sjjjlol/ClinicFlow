using System;
using static ClinicFlow.Ois.DomainRules;

namespace ClinicFlow.Ois
{
    public class TreatmentDelivery : IOisEntity
    {
        public string Id { get; internal set; } = Guid.NewGuid().ToString();
        public int Version { get; internal set; } = 1;
        public string ReservationId { get; internal set; } = "";
        public string PlanId { get; internal set; } = "";
        public string MachineId { get; internal set; } = "";
        public string StartedBy { get; internal set; } = "";
        public DateTime StartedUtc { get; internal set; }
        public string Outcome { get; internal set; } = "InProgress";
        public string? FinishedBy { get; internal set; }
        public DateTime? FinishedUtc { get; internal set; }
        public string Note { get; internal set; } = "";
        internal void Finish(string outcome, string note, string actor, DateTime now, bool machineAvailable)
        {
            Need(Outcome == "InProgress", "signed", "执行结果已签署，只能追加更正");
            Need(outcome == "Completed" || outcome == "Interrupted", "invalid_result", "请选择完成或中断", 400);
            var text = Text(note);
            Need(outcome != "Completed" || machineAvailable, "machine_unavailable", "机器已停用，请记录中断并评估");
            Note = text;
            Outcome = outcome;
            FinishedBy = actor;
            FinishedUtc = now;
            Version++;
        }
    }
}

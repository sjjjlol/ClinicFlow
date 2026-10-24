using System;
using System.Collections.Generic;
using ClinicFlow.Ois.Events;
using static ClinicFlow.Ois.DomainRules;

namespace ClinicFlow.Ois
{
    public class TreatmentDelivery : IOisEntity, IHasDomainEvents
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
        public string ReservationId { get; internal set; } = "";
        public string PlanId { get; internal set; } = "";
        public string MachineId { get; internal set; } = "";
        public string StartedBy { get; internal set; } = "";
        public DateTime StartedUtc { get; internal set; }
        public string Outcome { get; internal set; } = "InProgress";
        public string? FinishedBy { get; internal set; }
        public DateTime? FinishedUtc { get; internal set; }
        public string Note { get; internal set; } = "";
        public void Finish(string outcome, string note, string actor, DateTime now, bool machineAvailable)
        {
            Need(Outcome == "InProgress", "signed", "执行结果已签署，只能追加更正");
            Need(outcome == "Completed" || outcome == "Interrupted", "invalid_result", "请选择完成或中断", BusinessErrorKind.InvalidInput);
            var text = Text(note);
            Need(outcome != "Completed" || machineAvailable, "machine_unavailable", "机器已停用，请记录中断并评估");
            Note = text;
            events.Add(new TreatmentFinished(Id, ReservationId, PlanId, outcome, note, actor, now));
            Outcome = outcome;
            FinishedBy = actor;
            FinishedUtc = now;
            Version++;
        }
        public static TreatmentDelivery Start(string reservationId, string planId, string machineId, string actor, DateTime now) =>
            new TreatmentDelivery { ReservationId = reservationId, PlanId = planId, MachineId = machineId, StartedBy = actor, StartedUtc = now };
    }
}

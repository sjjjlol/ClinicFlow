using System;
using static ClinicFlow.Ois.DomainRules;

namespace ClinicFlow.Ois
{
    public class OisTask : IOisEntity
    {
        public string Id { get; internal set; } = Guid.NewGuid().ToString();
        public int Version { get; internal set; } = 1;
        public string TargetId { get; internal set; } = "";
        public string Role { get; internal set; } = "";
        public string Reason { get; internal set; } = "";
        public string? ReadBy { get; internal set; }
        public string? ResolvedBy { get; internal set; }
        public string Resolution { get; internal set; } = "";
        public DateTime CreatedUtc { get; internal set; }
        public DateTime? ResolvedUtc { get; internal set; }
        public static OisTask Create(string targetId, string role, string reason, DateTime now) =>
            new OisTask { TargetId = targetId, Role = role, Reason = reason, CreatedUtc = now };
        public void Read(string actor)
        { Need(ResolvedBy == null, "invalid_state", "待办已经处理"); ReadBy = actor; Version++; }
        public void Resolve(string actor, string text, DateTime now)
        {
            Need(ResolvedBy == null, "invalid_state", "待办已经处理");
            Resolution = Text(text); ReadBy = actor; ResolvedBy = actor; ResolvedUtc = now; Version++;
        }
    }
}

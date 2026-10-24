using System;

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
    }
}

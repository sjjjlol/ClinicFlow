using System;

namespace ClinicFlow.Ois
{
    public class OisAudit
    {
        public long Id { get; internal set; }
        public string Actor { get; internal set; } = "";
        public string Role { get; internal set; } = "";
        public string Action { get; internal set; } = "";
        public string TargetId { get; internal set; } = "";
        public string Detail { get; internal set; } = "";
        public string CorrelationId { get; internal set; } = "";
        public DateTime AtUtc { get; internal set; }
    }

}

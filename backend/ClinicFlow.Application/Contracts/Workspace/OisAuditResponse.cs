using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicFlow.Ois
{
    public sealed class OisAuditResponse
    {
        public long Id { get; set; }
        public string Actor { get; set; } = "";
        public string Role { get; set; } = "";
        public string Action { get; set; } = "";
        public string TargetId { get; set; } = "";
        public string Detail { get; set; } = "";
        public string CorrelationId { get; set; } = "";
        public DateTime AtUtc { get; set; }
    }
}

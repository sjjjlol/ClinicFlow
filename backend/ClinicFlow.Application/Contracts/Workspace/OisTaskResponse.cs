using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicFlow.Ois
{
    public sealed class OisTaskResponse
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string TargetId { get; set; } = "";
        public string Role { get; set; } = "";
        public string Reason { get; set; } = "";
        public string? ReadBy { get; set; }
        public string? ResolvedBy { get; set; }
        public string Resolution { get; set; } = "";
        public DateTime CreatedUtc { get; set; }
        public DateTime? ResolvedUtc { get; set; }
    }
}

using System;

namespace ClinicFlow.Application.Commands
{
    [ClinicFlow.Ois.BusinessOperation("book")]
    public sealed class BookMaintenanceCommand
    {
        public string ResourceId { get; set; } = "";
        public DateTimeOffset? StartUtc { get; set; }
        public DateTimeOffset? EndUtc { get; set; }
        public string Reason { get; set; } = "";
        public string Kind => "Maintenance";
    }
}

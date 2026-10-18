using System;
using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    [BusinessOperation("book")]
    public sealed class BookMaintenanceRequest
    {
        public string ResourceId { get; set; } = "";
        public DateTimeOffset? StartUtc { get; set; }
        public DateTimeOffset? EndUtc { get; set; }
        public string Reason { get; set; } = "";
        [JsonIgnore]
        public string Kind => "Maintenance";
    }
}

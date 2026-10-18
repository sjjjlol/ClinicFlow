using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    [BusinessOperation("machine")]
    public sealed class SetMachineAvailabilityRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public bool Available { get; set; } = true;
        public string Reason { get; set; } = "";
    }
}

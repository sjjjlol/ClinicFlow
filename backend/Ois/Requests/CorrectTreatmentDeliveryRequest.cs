using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    [BusinessOperation("correction")]
    public sealed class CorrectTreatmentDeliveryRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Text { get; set; } = "";
        public string Reason { get; set; } = "";
    }
}

using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    [BusinessOperation("finish")]
    public sealed class FinishTreatmentRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Outcome { get; set; } = "";
        public string Text { get; set; } = "";
    }
}

using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    [BusinessOperation("consult")]
    public sealed class CompleteConsultationRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string CaseId { get; set; } = "";
    }
}

using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    [BusinessOperation("start")]
    public sealed class StartTreatmentRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string PlanId { get; set; } = "";
        public bool IdentityChecked { get; set; }
    }
}

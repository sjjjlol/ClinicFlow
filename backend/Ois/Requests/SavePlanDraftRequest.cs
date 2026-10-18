using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    [BusinessOperation("save-draft")]
    public sealed class SavePlanDraftRequest
    {
        [JsonIgnore]
        public string CaseId { get; set; } = "";
        public int Version { get; set; }
        public string DraftJson { get; set; } = "";
        public int DraftVersion { get; set; }
        [JsonIgnore]
        public string Kind => "plan";
    }
}

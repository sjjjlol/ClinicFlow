using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class SavePlanDraftRequest
    {
        [JsonIgnore]
        public string CaseId { get; set; } = "";
        public int Version { get; set; }
        public string DraftJson { get; set; } = "";
        public int DraftVersion { get; set; }
        [JsonIgnore]
        public string Kind => "plan";
        public ClinicFlow.Application.Commands.SavePlanDraftCommand ToCommand() =>
            new ClinicFlow.Application.Commands.SavePlanDraftCommand
            {
                CaseId = CaseId,
                Version = Version,
                DraftJson = DraftJson,
                DraftVersion = DraftVersion,
            };
    }
}

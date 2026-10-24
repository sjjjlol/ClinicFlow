using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class SavePrescriptionDraftRequest
    {
        [JsonIgnore]
        public string CaseId { get; set; } = "";
        public int Version { get; set; }
        public string DraftJson { get; set; } = "";
        public int DraftVersion { get; set; }
        [JsonIgnore]
        public string Kind => "prescribe";
        public ClinicFlow.Application.Commands.SavePrescriptionDraftCommand ToCommand() =>
            new ClinicFlow.Application.Commands.SavePrescriptionDraftCommand
            {
                CaseId = CaseId,
                Version = Version,
                DraftJson = DraftJson,
                DraftVersion = DraftVersion,
            };
    }
}

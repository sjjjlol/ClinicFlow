
namespace ClinicFlow.Application.Commands
{
    [ClinicFlow.Ois.BusinessOperation("save-draft")]
    public sealed class SavePrescriptionDraftCommand
    {
        public string CaseId { get; set; } = "";
        public int Version { get; set; }
        public string DraftJson { get; set; } = "";
        public int DraftVersion { get; set; }
        public string Kind => "prescribe";
    }
}

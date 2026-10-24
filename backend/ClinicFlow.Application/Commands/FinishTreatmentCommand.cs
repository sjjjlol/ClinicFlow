
namespace ClinicFlow.Application.Commands
{
    [ClinicFlow.Ois.BusinessOperation("finish")]
    public sealed class FinishTreatmentCommand
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Outcome { get; set; } = "";
        public string Text { get; set; } = "";
    }
}


namespace ClinicFlow.Application.Commands
{
    [ClinicFlow.Ois.BusinessOperation("correction")]
    public sealed class CorrectPlanCommand
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Text { get; set; } = "";
        public string Reason { get; set; } = "";
    }
}

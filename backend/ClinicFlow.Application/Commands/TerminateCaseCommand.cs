
namespace ClinicFlow.Application.Commands
{
    [ClinicFlow.Ois.BusinessOperation("terminate")]
    public sealed class TerminateCaseCommand
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Reason { get; set; } = "";
    }
}


namespace ClinicFlow.Application.Commands
{
    [ClinicFlow.Ois.BusinessOperation("no-show")]
    public sealed class RecordNoShowCommand
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Reason { get; set; } = "";
    }
}

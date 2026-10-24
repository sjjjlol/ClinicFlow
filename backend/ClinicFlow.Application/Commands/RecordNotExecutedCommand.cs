
namespace ClinicFlow.Application.Commands
{
    [ClinicFlow.Ois.BusinessOperation("not-executed")]
    public sealed class RecordNotExecutedCommand
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Reason { get; set; } = "";
    }
}

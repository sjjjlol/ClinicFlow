
namespace ClinicFlow.Application.Commands
{
    [ClinicFlow.Ois.BusinessOperation("read-task")]
    public sealed class ReadTaskCommand
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
    }
}


namespace ClinicFlow.Application.Commands
{
    [ClinicFlow.Ois.BusinessOperation("resolve-task")]
    public sealed class ResolveTaskCommand
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Text { get; set; } = "";
    }
}

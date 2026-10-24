
namespace ClinicFlow.Application.Commands
{
    [ClinicFlow.Ois.BusinessOperation("note")]
    public sealed class AddCaseNoteCommand
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Text { get; set; } = "";
    }
}

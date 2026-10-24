using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class AddCaseNoteRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Text { get; set; } = "";
        public ClinicFlow.Application.Commands.AddCaseNoteCommand ToCommand() =>
            new ClinicFlow.Application.Commands.AddCaseNoteCommand
            {
                Id = Id,
                Version = Version,
                Text = Text,
            };
    }
}

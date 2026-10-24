using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class ResolveTaskRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Text { get; set; } = "";
        public ClinicFlow.Application.Commands.ResolveTaskCommand ToCommand() =>
            new ClinicFlow.Application.Commands.ResolveTaskCommand
            {
                Id = Id,
                Version = Version,
                Text = Text,
            };
    }
}

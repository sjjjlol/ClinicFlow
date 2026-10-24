using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class ReadTaskRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public ClinicFlow.Application.Commands.ReadTaskCommand ToCommand() =>
            new ClinicFlow.Application.Commands.ReadTaskCommand
            {
                Id = Id,
                Version = Version,
            };
    }
}

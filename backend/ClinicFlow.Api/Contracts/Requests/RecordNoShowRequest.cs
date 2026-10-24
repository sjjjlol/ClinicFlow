using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class RecordNoShowRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Reason { get; set; } = "";
        public ClinicFlow.Application.Commands.RecordNoShowCommand ToCommand() =>
            new ClinicFlow.Application.Commands.RecordNoShowCommand
            {
                Id = Id,
                Version = Version,
                Reason = Reason,
            };
    }
}

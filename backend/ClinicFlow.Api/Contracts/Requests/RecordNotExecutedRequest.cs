using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class RecordNotExecutedRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Reason { get; set; } = "";
        public ClinicFlow.Application.Commands.RecordNotExecutedCommand ToCommand() =>
            new ClinicFlow.Application.Commands.RecordNotExecutedCommand
            {
                Id = Id,
                Version = Version,
                Reason = Reason,
            };
    }
}

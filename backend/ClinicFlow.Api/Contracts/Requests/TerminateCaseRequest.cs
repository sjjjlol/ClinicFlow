using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class TerminateCaseRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Reason { get; set; } = "";
        public ClinicFlow.Application.Commands.TerminateCaseCommand ToCommand() =>
            new ClinicFlow.Application.Commands.TerminateCaseCommand
            {
                Id = Id,
                Version = Version,
                Reason = Reason,
            };
    }
}

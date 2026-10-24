using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class RestoreReservationRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Reason { get; set; } = "";
        public ClinicFlow.Application.Commands.RestoreReservationCommand ToCommand() =>
            new ClinicFlow.Application.Commands.RestoreReservationCommand
            {
                Id = Id,
                Version = Version,
                Reason = Reason,
            };
    }
}

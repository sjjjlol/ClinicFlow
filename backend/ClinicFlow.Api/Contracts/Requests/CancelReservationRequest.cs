using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class CancelReservationRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Reason { get; set; } = "";
        public ClinicFlow.Application.Commands.CancelReservationCommand ToCommand() =>
            new ClinicFlow.Application.Commands.CancelReservationCommand
            {
                Id = Id,
                Version = Version,
                Reason = Reason,
            };
    }
}

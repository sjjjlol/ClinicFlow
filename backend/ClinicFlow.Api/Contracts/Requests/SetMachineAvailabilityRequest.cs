using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class SetMachineAvailabilityRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public bool Available { get; set; } = true;
        public string Reason { get; set; } = "";
        public ClinicFlow.Application.Commands.SetMachineAvailabilityCommand ToCommand() =>
            new ClinicFlow.Application.Commands.SetMachineAvailabilityCommand
            {
                Id = Id,
                Version = Version,
                Available = Available,
                Reason = Reason,
            };
    }
}

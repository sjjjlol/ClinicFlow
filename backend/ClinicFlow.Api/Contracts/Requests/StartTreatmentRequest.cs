using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class StartTreatmentRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string PlanId { get; set; } = "";
        public bool IdentityChecked { get; set; }
        public ClinicFlow.Application.Commands.StartTreatmentCommand ToCommand() =>
            new ClinicFlow.Application.Commands.StartTreatmentCommand
            {
                Id = Id,
                Version = Version,
                PlanId = PlanId,
                IdentityChecked = IdentityChecked,
            };
    }
}

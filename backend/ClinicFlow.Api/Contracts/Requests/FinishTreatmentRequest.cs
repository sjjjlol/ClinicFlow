using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class FinishTreatmentRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Outcome { get; set; } = "";
        public string Text { get; set; } = "";
        public ClinicFlow.Application.Commands.FinishTreatmentCommand ToCommand() =>
            new ClinicFlow.Application.Commands.FinishTreatmentCommand
            {
                Id = Id,
                Version = Version,
                Outcome = Outcome,
                Text = Text,
            };
    }
}

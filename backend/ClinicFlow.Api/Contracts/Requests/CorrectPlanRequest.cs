using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class CorrectPlanRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Text { get; set; } = "";
        public string Reason { get; set; } = "";
        public ClinicFlow.Application.Commands.CorrectPlanCommand ToCommand() =>
            new ClinicFlow.Application.Commands.CorrectPlanCommand
            {
                Id = Id,
                Version = Version,
                Text = Text,
                Reason = Reason,
            };
    }
}

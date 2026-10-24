using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class ReviewPlanRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public ClinicFlow.Application.Commands.ReviewPlanCommand ToCommand() =>
            new ClinicFlow.Application.Commands.ReviewPlanCommand
            {
                Id = Id,
                Version = Version,
            };
    }
}

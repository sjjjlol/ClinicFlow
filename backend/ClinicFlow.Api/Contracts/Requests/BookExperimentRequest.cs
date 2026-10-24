using System;
using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class BookExperimentRequest
    {
        public string ResourceId { get; set; } = "";
        public DateTimeOffset? StartUtc { get; set; }
        public DateTimeOffset? EndUtc { get; set; }
        public string Reason { get; set; } = "";
        public string PlanId { get; set; } = "";
        [JsonIgnore]
        public string Kind => "Experiment";
        public ClinicFlow.Application.Commands.BookExperimentCommand ToCommand() =>
            new ClinicFlow.Application.Commands.BookExperimentCommand
            {
                ResourceId = ResourceId,
                StartUtc = StartUtc,
                EndUtc = EndUtc,
                Reason = Reason,
                PlanId = PlanId,
            };
    }
}

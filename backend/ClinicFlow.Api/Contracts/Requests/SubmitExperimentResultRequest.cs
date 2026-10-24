using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class SubmitExperimentResultRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Outcome { get; set; } = "";
        public string Text { get; set; } = "";
        public string FailureType { get; set; } = "";
        public string MeasuredValues { get; set; } = "";
        public string ResultAttachment { get; set; } = "";
        public ClinicFlow.Application.Commands.SubmitExperimentResultCommand ToCommand() =>
            new ClinicFlow.Application.Commands.SubmitExperimentResultCommand
            {
                Id = Id,
                Version = Version,
                Outcome = Outcome,
                Text = Text,
                FailureType = FailureType,
                MeasuredValues = MeasuredValues,
                ResultAttachment = ResultAttachment,
            };
    }
}

using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class RegisterPlanRequest
    {
        [JsonIgnore]
        public string CaseId { get; set; } = "";
        public int Version { get; set; }
        public string ResourceId { get; set; } = "";
        public string Text { get; set; } = "";
        public string Reason { get; set; } = "";
        public string Attachment { get; set; } = "";
        public string VerificationMethod { get; set; } = "";
        public string Equipment { get; set; } = "";
        public string MeasurementConditions { get; set; } = "";
        public string ExpectedValues { get; set; } = "";
        public string StandardName { get; set; } = "";
        public string StandardVersion { get; set; } = "";
        public string AcceptanceCriteria { get; set; } = "";
        public string AllowedDeviation { get; set; } = "";
        public ClinicFlow.Application.Commands.RegisterPlanCommand ToCommand() =>
            new ClinicFlow.Application.Commands.RegisterPlanCommand
            {
                CaseId = CaseId,
                Version = Version,
                ResourceId = ResourceId,
                Text = Text,
                Reason = Reason,
                Attachment = Attachment,
                VerificationMethod = VerificationMethod,
                Equipment = Equipment,
                MeasurementConditions = MeasurementConditions,
                ExpectedValues = ExpectedValues,
                StandardName = StandardName,
                StandardVersion = StandardVersion,
                AcceptanceCriteria = AcceptanceCriteria,
                AllowedDeviation = AllowedDeviation,
            };
    }
}

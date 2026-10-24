using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class CompleteConsultationRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string CaseId { get; set; } = "";
        public ClinicFlow.Application.Commands.CompleteConsultationCommand ToCommand() =>
            new ClinicFlow.Application.Commands.CompleteConsultationCommand
            {
                Id = Id,
                Version = Version,
                CaseId = CaseId,
            };
    }
}

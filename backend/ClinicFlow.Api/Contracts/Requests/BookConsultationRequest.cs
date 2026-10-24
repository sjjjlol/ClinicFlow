using System;
using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class BookConsultationRequest
    {
        public string ResourceId { get; set; } = "";
        public DateTimeOffset? StartUtc { get; set; }
        public DateTimeOffset? EndUtc { get; set; }
        public string Reason { get; set; } = "";
        public int PatientId { get; set; }
        public string CaseId { get; set; } = "";
        [JsonIgnore]
        public string Kind => "Consultation";
        public ClinicFlow.Application.Commands.BookConsultationCommand ToCommand() =>
            new ClinicFlow.Application.Commands.BookConsultationCommand
            {
                ResourceId = ResourceId,
                StartUtc = StartUtc,
                EndUtc = EndUtc,
                Reason = Reason,
                PatientId = PatientId,
                CaseId = CaseId,
            };
    }
}

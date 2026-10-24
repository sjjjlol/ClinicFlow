using System;
using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class BookTreatmentRequest
    {
        public string ResourceId { get; set; } = "";
        public DateTimeOffset? StartUtc { get; set; }
        public DateTimeOffset? EndUtc { get; set; }
        public string Reason { get; set; } = "";
        public int PatientId { get; set; }
        public string CaseId { get; set; } = "";
        [JsonIgnore]
        public string Kind => "Treatment";
        public ClinicFlow.Application.Commands.BookTreatmentCommand ToCommand() =>
            new ClinicFlow.Application.Commands.BookTreatmentCommand
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

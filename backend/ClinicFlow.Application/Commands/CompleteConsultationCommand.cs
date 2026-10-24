
namespace ClinicFlow.Application.Commands
{
    [ClinicFlow.Ois.BusinessOperation("consult")]
    public sealed class CompleteConsultationCommand
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string CaseId { get; set; } = "";
    }
}

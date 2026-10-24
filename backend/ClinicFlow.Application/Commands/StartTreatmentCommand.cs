
namespace ClinicFlow.Application.Commands
{
    [ClinicFlow.Ois.BusinessOperation("start")]
    public sealed class StartTreatmentCommand
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string PlanId { get; set; } = "";
        public bool IdentityChecked { get; set; }
    }
}


namespace ClinicFlow.Application.Commands
{
    [ClinicFlow.Ois.BusinessOperation("review")]
    public sealed class ReviewPlanCommand
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
    }
}


namespace ClinicFlow.Application.Commands
{
    [ClinicFlow.Ois.BusinessOperation("machine")]
    public sealed class SetMachineAvailabilityCommand
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public bool Available { get; set; } = true;
        public string Reason { get; set; } = "";
    }
}

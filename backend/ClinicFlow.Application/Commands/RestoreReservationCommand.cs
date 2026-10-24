
namespace ClinicFlow.Application.Commands
{
    [ClinicFlow.Ois.BusinessOperation("restore")]
    public sealed class RestoreReservationCommand
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Reason { get; set; } = "";
    }
}


namespace ClinicFlow.Application.Commands
{
    [ClinicFlow.Ois.BusinessOperation("cancel")]
    public sealed class CancelReservationCommand
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Reason { get; set; } = "";
    }
}

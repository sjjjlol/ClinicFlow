using System;

namespace ClinicFlow.Ois
{
    // Only effective claims live here. Reservation and audit retain the business history.
    public sealed class ReservationOccupancy
    {
        public string SubjectType { get; set; } = "";
        public string SubjectId { get; set; } = "";
        public DateTime SlotStartUtc { get; set; }
        public string ReservationId { get; set; } = "";
    }

    public sealed class ActiveExecution
    {
        public string SubjectType { get; set; } = "";
        public string SubjectId { get; set; } = "";
        public string DeliveryId { get; set; } = "";
    }

    internal sealed class OisCommandLock
    {
        public string Id { get; set; } = "";
    }
}

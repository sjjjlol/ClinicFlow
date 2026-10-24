using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicFlow.Ois
{
    public sealed class ReservationResponse
    {
        public string Id { get; }
        public int Version { get; }
        public string Kind { get; }
        public int? PatientId { get; }
        public string? CaseId { get; }
        public string? PlanId { get; }
        public string ResourceId { get; }
        public DateTime StartUtc { get; }
        public DateTime EndUtc { get; }
        public string Status { get; }
        public string Reason { get; }
        public string MeasuredValues { get; }
        public string ResultAttachment { get; }
        public string ValidationOutcome { get; }
        public string ValidationNote { get; }
        public string FailureType { get; }
        public string SignedBy { get; }
        public DateTime? SignedUtc { get; }
        public ReservationResponse(Reservation booking, PhysicalValidation? validation)
        {
            Id = booking.Id;
            Version = booking.Version;
            Kind = booking.Kind;
            PatientId = booking.PatientId;
            CaseId = booking.CaseId;
            PlanId = booking.PlanId;
            ResourceId = booking.ResourceId;
            StartUtc = booking.StartUtc;
            EndUtc = booking.EndUtc;
            Status = booking.Status;
            Reason = booking.Reason;
            MeasuredValues = validation?.MeasuredValues ?? "";
            ResultAttachment = validation?.ResultAttachment ?? "";
            ValidationOutcome = validation?.ValidationOutcome ?? "";
            ValidationNote = validation?.ValidationNote ?? "";
            FailureType = validation?.FailureType ?? "";
            SignedBy = validation?.SignedBy ?? "";
            SignedUtc = validation?.SignedUtc;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicFlow.Ois
{
    internal static class ReservationProjection
    {
        internal static ReservationResponse Project(Reservation booking, PhysicalValidation? validation) => new ReservationResponse
        {
            Id = booking.Id,
            Version = booking.Version,
            Kind = booking.Kind,
            PatientId = booking.PatientId,
            CaseId = booking.CaseId,
            PlanId = booking.PlanId,
            ResourceId = booking.ResourceId,
            StartUtc = booking.StartUtc,
            EndUtc = booking.EndUtc,
            Status = booking.Status,
            Reason = booking.Reason,
            MeasuredValues = validation?.MeasuredValues ?? "",
            ResultAttachment = validation?.ResultAttachment ?? "",
            ValidationOutcome = validation?.ValidationOutcome ?? "",
            ValidationNote = validation?.ValidationNote ?? "",
            FailureType = validation?.FailureType ?? "",
            SignedBy = validation?.SignedBy ?? "",
            SignedUtc = validation?.SignedUtc,
        };
    }
}

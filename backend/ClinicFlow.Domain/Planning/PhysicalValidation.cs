using System;
using static ClinicFlow.Ois.DomainRules;

namespace ClinicFlow.Ois
{
    public class PhysicalValidation
    {
        public string ReservationId { get; private set; } = "";
        public string MeasuredValues { get; private set; } = "";
        public string ResultAttachment { get; private set; } = "";
        public string ValidationOutcome { get; private set; } = "";
        public string ValidationNote { get; private set; } = "";
        public string FailureType { get; private set; } = "";
        public string SignedBy { get; private set; } = "";
        public DateTime? SignedUtc { get; private set; }
        private PhysicalValidation() { }
        public static PhysicalValidation Sign(string reservationId, string outcome, string note,
            string failureType, string measuredValues, string attachment, string actor, DateTime now)
        {
            Text(measuredValues);
            Text(attachment);
            Text(note);
            Need(outcome == "Passed" || outcome == "Failed", "invalid_result", "请选择通过或不通过", BusinessErrorKind.InvalidInput);
            Need(outcome != "Failed" || failureType == "Plan" || failureType == "Clinical" || failureType == "Equipment",
                "invalid_failure", "请选择失败类型", BusinessErrorKind.InvalidInput);
            return new PhysicalValidation
            {
                ReservationId = reservationId, ValidationOutcome = outcome, ValidationNote = note,
                FailureType = outcome == "Failed" ? failureType : "", MeasuredValues = measuredValues,
                ResultAttachment = attachment, SignedBy = actor, SignedUtc = now,
            };
        }
    }
}

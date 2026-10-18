using System;
using System.Linq;
using ClinicFlow.Ois.Requests;

namespace ClinicFlow.Ois.Operations
{
    public sealed class ConsultationsOperations
    {
        private readonly OisState state;
        public ConsultationsOperations(OisState state) => this.state = state;

        public string CompleteConsultation(CompleteConsultationRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            var booking = state.Current(state.Reservations, request.Id, request.Version);
            state.Need(
                booking.Kind == "Consultation"
                    && booking.Status == "Booked"
                    && now >= booking.StartUtc,
                "invalid_state",
                "门诊尚未开始或已处理"
            );
            state.Need(
                booking.ResourceId == actor,
                "wrong_clinician",
                "请由预约接诊人员登记完成",
                403
            );
            var care = state.Get(state.Cases, request.CaseId);
            state.Need(
                care.PatientId == booking.PatientId && care.CurrentPrescriptionId != null,
                "clinical_record_required",
                "请先为该患者建立档案并签署诊疗记录"
            );
            booking.CaseId = care.Id;
            booking.Status = "Completed";
            booking.Version++;
            return booking.Id;
        }

    }
}

using System;
using System.Linq;
using static ClinicFlow.Ois.DomainRules;
using ClinicFlow.Application.Commands;

namespace ClinicFlow.Ois.Modules.Scheduling
{
    public sealed partial class SchedulingModule
    {

        public string CompleteConsultation(CompleteConsultationCommand request, string actor, string role, DateTime now)
        {
            Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", BusinessErrorKind.Forbidden);
            var booking = Current(reservations, request.Id, request.Version);
            Need(
                booking.Kind == "Consultation"
                    && booking.Status == "Booked"
                    && now >= booking.StartUtc,
                "invalid_state",
                "门诊尚未开始或已处理"
            );
            Need(
                booking.ResourceId == actor,
                "wrong_clinician",
                "请由预约接诊人员登记完成", BusinessErrorKind.Forbidden);
            var care = clinical.Case(request.CaseId);
            Need(
                care.PatientId == booking.PatientId && care.CurrentPrescriptionId != null,
                "clinical_record_required",
                "请先为该患者建立档案并签署诊疗记录"
            );
            booking.CompleteConsultation(care.Id, now);
            return booking.Id;
        }

    }
}

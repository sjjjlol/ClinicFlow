using System;
using System.Linq;
using System.Collections.Generic;
using static ClinicFlow.Ois.DomainRules;
namespace ClinicFlow.Ois
{
    public static class SchedulePolicy
    {
        public static void CheckSlot(Reservation r, DateTime now, List<OisResource> resources, List<Reservation> reservations, List<TreatmentDelivery> deliveries)
        {
            if (r.StartUtc.Ticks % TimeSpan.FromMinutes(15).Ticks != 0
                || r.EndUtc.Ticks % TimeSpan.FromMinutes(15).Ticks != 0
                || r.EndUtc <= r.StartUtc || r.EndUtc - r.StartUtc > TimeSpan.FromHours(4))
                throw new BusinessException("invalid_time", "起止时间必须对齐15分钟，时长为15分钟至4小时", BusinessErrorKind.InvalidInput);
            Need(r.StartUtc > now, "past_slot", "请选择未来时段");
            var resource = Get(resources, r.ResourceId);
            Need(
                resource.Kind == (r.Kind == "Consultation" ? "Clinician" : "Machine"),
                "invalid_resource",
                "资源类型与活动不匹配", BusinessErrorKind.InvalidInput);
            Need(resource.Available, "machine_unavailable", "机器已停用");
            Need(
                !deliveries.Any(x => x.Outcome == "InProgress" && x.MachineId == r.ResourceId),
                "in_progress",
                "机器有尚未结束的治疗，请先处理"
            );
            Need(
                !reservations.Any(x =>
                    x.Id != r.Id
                    && x.Status != "Cancelled"
                    && x.StartUtc < r.EndUtc
                    && r.StartUtc < x.EndUtc
                    && (
                        x.ResourceId == r.ResourceId
                        || (r.PatientId != null && x.PatientId == r.PatientId)
                    )
                ),
                "slot_conflict",
                "患者或资源时段冲突，请刷新日历"
            );
        }

    }
}

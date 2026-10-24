using System;
using System.Linq;
using static ClinicFlow.Ois.DomainRules;
using ClinicFlow.Ois.Requests;

namespace ClinicFlow.Ois.Modules.Scheduling
{
    public sealed partial class SchedulingModule
    {

        public string StartTreatment(StartTreatmentRequest request, string actor, string role, DateTime now)
        {
            Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            var booking = Current(reservations, request.Id, request.Version);
            Need(
                booking.Kind == "Treatment" && booking.Status == "Booked",
                "invalid_state",
                "预约不可开始治疗"
            );
            Need(
                now >= booking.StartUtc && now < booking.EndUtc,
                "outside_slot",
                "请在预约时段内开始治疗，超时需改期"
            );
            Need(request.IdentityChecked, "identity_required", "请先核对患者身份");
            var blockers = Blockers(booking.Id);
            Need(blockers.Count == 0, "not_released", string.Join("；", blockers));
            Need(
                !deliveries.Any(x =>
                    x.Outcome == "InProgress"
                    && (
                        x.MachineId == booking.ResourceId
                        || Get(reservations, x.ReservationId).PatientId == booking.PatientId
                    )
                ),
                "in_progress",
                "患者或机器仍有未结束的治疗"
            );
            var care = clinical.Case(booking.CaseId!);
            Need(
                request.PlanId == care.CurrentPlanId,
                "plan_changed",
                "计划版本已变更，请重新核对当前计划"
            );
            var delivery = new TreatmentDelivery
            {
                ReservationId = booking.Id,
                PlanId = care.CurrentPlanId!,
                MachineId = booking.ResourceId,
                StartedBy = actor,
                StartedUtc = now,
            };
            deliveries.Add(delivery);
            booking.StartTreatment();
            return delivery.Id;
        }

        public string FinishTreatment(FinishTreatmentRequest request, string actor, string role, DateTime now)
        {
            Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            var delivery = Current(deliveries, request.Id, request.Version);
            delivery.Finish(request.Outcome, request.Text, actor, now, Machine(delivery.MachineId).Available);
            var booking = Get(reservations, delivery.ReservationId);
            booking.FinishTreatment();
            if (request.Outcome == "Interrupted")
                support.AddTask(delivery.Id, "Clinician", "治疗中断，需评估后续安排：" + request.Text, now);
            return delivery.Id;
        }

        public string RecordNotExecuted(RecordNotExecutedRequest request, string actor, string role, DateTime now)
        {
            Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            var booking = Current(reservations, request.Id, request.Version);
            Need(
                booking.Kind == "Treatment"
                    && booking.Status == "Booked"
                    && now >= booking.StartUtc,
                "invalid_state",
                "该预约不可登记未执行"
            );
            booking.Reason = Text(request.Reason);
            booking.Status = "NotExecuted";
            booking.Version++;
            support.AddTask(booking.Id, "Clinician", "治疗未执行，需评估：" + request.Reason, now);
            return booking.Id;
        }

        public string CorrectTreatmentDelivery(CorrectTreatmentDeliveryRequest request, string actor, string role, DateTime now)
        {
            Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            Text(request.Text);
            Text(request.Reason);
            var delivery = Current(deliveries, request.Id, request.Version);
            Need(delivery.Outcome != "InProgress", "not_signed", "请先结束治疗记录");
            support.AddTask(delivery.Id, "Clinician", "治疗记录更正需评估：" + request.Text, now);
            return request.Id;
        }

    }
}

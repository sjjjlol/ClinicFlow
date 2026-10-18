using System;
using System.Linq;
using ClinicFlow.Ois.Requests;

namespace ClinicFlow.Ois.Operations
{
    public sealed class TreatmentDeliveriesOperations
    {
        private readonly OisState state;
        public TreatmentDeliveriesOperations(OisState state) => this.state = state;

        public string StartTreatment(StartTreatmentRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            var booking = state.Current(state.Reservations, request.Id, request.Version);
            state.Need(
                booking.Kind == "Treatment" && booking.Status == "Booked",
                "invalid_state",
                "预约不可开始治疗"
            );
            state.Need(
                now >= booking.StartUtc && now < booking.EndUtc,
                "outside_slot",
                "请在预约时段内开始治疗，超时需改期"
            );
            state.Need(request.IdentityChecked, "identity_required", "请先核对患者身份");
            var blockers = state.Blockers(booking);
            state.Need(blockers.Count == 0, "not_released", string.Join("；", blockers));
            state.Need(
                !state.Deliveries.Any(x =>
                    x.Outcome == "InProgress"
                    && (
                        x.MachineId == booking.ResourceId
                        || state.Get(state.Reservations, x.ReservationId).PatientId == booking.PatientId
                    )
                ),
                "in_progress",
                "患者或机器仍有未结束的治疗"
            );
            var care = state.Get(state.Cases, booking.CaseId!);
            state.Need(
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
            state.Deliveries.Add(delivery);
            booking.Status = "InProgress";
            booking.Version++;
            return delivery.Id;
        }

        public string FinishTreatment(FinishTreatmentRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            var delivery = state.Current(state.Deliveries, request.Id, request.Version);
            state.Need(delivery.Outcome == "InProgress", "signed", "执行结果已签署，只能追加更正");
            state.Need(
                (request.Outcome == "Completed" || request.Outcome == "Interrupted"),
                "invalid_result",
                "请选择完成或中断",
                400
            );
            delivery.Note = state.Text(request.Text);
            state.Need(
                request.Outcome != "Completed" || state.Machine(delivery.MachineId).Available,
                "machine_unavailable",
                "机器已停用，请记录中断并评估"
            );
            delivery.Outcome = request.Outcome;
            delivery.FinishedBy = actor;
            delivery.FinishedUtc = now;
            delivery.Version++;
            var booking = state.Get(state.Reservations, delivery.ReservationId);
            booking.Status = "Completed";
            booking.Version++;
            if (request.Outcome == "Interrupted")
                state.AddTask(delivery.Id, "Clinician", "治疗中断，需评估后续安排：" + request.Text, now);
            return delivery.Id;
        }

        public string RecordNotExecuted(RecordNotExecutedRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            var booking = state.Current(state.Reservations, request.Id, request.Version);
            state.Need(
                booking.Kind == "Treatment"
                    && booking.Status == "Booked"
                    && now >= booking.StartUtc,
                "invalid_state",
                "该预约不可登记未执行"
            );
            booking.Reason = state.Text(request.Reason);
            booking.Status = "NotExecuted";
            booking.Version++;
            state.AddTask(booking.Id, "Clinician", "治疗未执行，需评估：" + request.Reason, now);
            return booking.Id;
        }

        public string CorrectTreatmentDelivery(CorrectTreatmentDeliveryRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            state.Text(request.Text);
            state.Text(request.Reason);
            var delivery = state.Current(state.Deliveries, request.Id, request.Version);
            state.Need(delivery.Outcome != "InProgress", "not_signed", "请先结束治疗记录");
            state.AddTask(delivery.Id, "Clinician", "治疗记录更正需评估：" + request.Text, now);
            return request.Id;
        }

    }
}

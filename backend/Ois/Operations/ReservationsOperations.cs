using System;
using System.Linq;
using ClinicFlow.Ois.Requests;

namespace ClinicFlow.Ois.Operations
{
    public sealed class ReservationsOperations
    {
        private readonly OisState state;
        public ReservationsOperations(OisState state) => this.state = state;

        public string RescheduleReservation(RescheduleReservationRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == ReservationRole(state.Get(state.Reservations, request.Id).Kind), "forbidden", "当前角色没有此操作权限", 403);
            var booking = state.Current(state.Reservations, request.Id, request.Version);
            state.Need(
                booking.Status == "Booked",
                "invalid_state",
                "当前状态不可改期或恢复"
            );
            var moved = new Reservation
            {
                Id = booking.Id,
                Kind = booking.Kind,
                PatientId = booking.PatientId,
                CaseId = booking.CaseId,
                PlanId = booking.PlanId,
                ResourceId = request.ResourceId,
                StartUtc = state.Time(request.StartUtc),
                EndUtc = state.Time(request.EndUtc),
            };
            ApplySchedule(booking, moved, request.Reason, now);
            return booking.Id;
        }

        public string RestoreReservation(RestoreReservationRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == ReservationRole(state.Get(state.Reservations, request.Id).Kind), "forbidden", "当前角色没有此操作权限", 403);
            var booking = state.Current(state.Reservations, request.Id, request.Version);
            state.Need(
                booking.Status == "Cancelled",
                "invalid_state",
                "当前状态不可改期或恢复"
            );
            var moved = new Reservation
            {
                Id = booking.Id,
                Kind = booking.Kind,
                PatientId = booking.PatientId,
                CaseId = booking.CaseId,
                PlanId = booking.PlanId,
                ResourceId = booking.ResourceId,
                StartUtc = booking.StartUtc,
                EndUtc = booking.EndUtc,
            };
            ApplySchedule(booking, moved, request.Reason, now);
            return booking.Id;
        }

        public string CancelReservation(CancelReservationRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == ReservationRole(state.Get(state.Reservations, request.Id).Kind), "forbidden", "当前角色没有此操作权限", 403);
            var booking = state.Current(state.Reservations, request.Id, request.Version);
            state.Need(booking.Status == "Booked", "invalid_state", "只可处理尚未执行的预约");
            booking.Reason = state.Text(request.Reason);
            booking.Status = "Cancelled";
            booking.Version++;
            return booking.Id;
        }

        public string RecordNoShow(RecordNoShowRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == ReservationRole(state.Get(state.Reservations, request.Id).Kind), "forbidden", "当前角色没有此操作权限", 403);
            var booking = state.Current(state.Reservations, request.Id, request.Version);
            state.Need(booking.Status == "Booked", "invalid_state", "只可处理尚未执行的预约");
            state.Need(
                booking.PatientId != null && now >= booking.EndUtc,
                "invalid_state",
                "预约结束后才可登记未到场"
            );
            booking.Reason = state.Text(request.Reason);
            booking.Status = "NoShow";
            booking.Version++;
            return booking.Id;
        }

        public string BookConsultation(BookConsultationRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == "Scheduler", "forbidden", "当前角色没有此操作权限", 403);
            return Book(request.Kind, request.PatientId, request.CaseId, "", request.ResourceId, request.StartUtc, request.EndUtc, request.Reason, now);
        }

        public string BookTreatment(BookTreatmentRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == "Scheduler", "forbidden", "当前角色没有此操作权限", 403);
            return Book(request.Kind, request.PatientId, request.CaseId, "", request.ResourceId, request.StartUtc, request.EndUtc, request.Reason, now);
        }

        public string BookExperiment(BookExperimentRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            return Book(request.Kind, 0, "", request.PlanId, request.ResourceId, request.StartUtc, request.EndUtc, request.Reason, now);
        }

        public string BookMaintenance(BookMaintenanceRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == "Physicist", "forbidden", "当前角色没有此操作权限", 403);
            return Book(request.Kind, 0, "", "", request.ResourceId, request.StartUtc, request.EndUtc, request.Reason, now);
        }

        private string Book(string kind, int patientId, string caseId, string planId, string resourceId, DateTimeOffset? startUtc, DateTimeOffset? endUtc, string reason, DateTime now)
        {
            state.Need(
                (kind == "Consultation" || kind == "Treatment" || kind == "Experiment" || kind == "Maintenance"),
                "invalid_kind",
                "预约类型无效",
                400
            );
            var booking = new Reservation
            {
                Kind = kind,
                ResourceId = resourceId,
                StartUtc = state.Time(startUtc),
                EndUtc = state.Time(endUtc),
                Reason = reason,
            };
            if (kind == "Consultation" || kind == "Treatment")
            {
                state.Need(
                    state.Patients.Any(x => x.Id == patientId),
                    "patient_missing",
                    "患者不存在",
                    404
                );
                booking.PatientId = patientId;
                if (kind == "Treatment" || caseId != "")
                {
                    var care = state.Get(state.Cases, caseId);
                    state.Need(
                        care.PatientId == patientId,
                        "patient_mismatch",
                        "诊疗档案与患者不匹配",
                        400
                    );
                    booking.CaseId = care.Id;
                }
            }
            if (kind == "Experiment")
            {
                var plan = state.Get(state.Plans, planId);
                state.CurrentPlan(plan);
                state.Need(state.CanExperiment(plan), "signed", "临床问题须修改处方；已通过计划不可重复验证");
                state.Need(plan.ReviewedBy != null, "review_required", "请先审核计划再安排实验");
                state.Need(
                    plan.MachineId == resourceId,
                    "machine_mismatch",
                    "实验机器须与计划一致"
                );
                state.Need(
                    !state.Reservations.Any(x =>
                        x.PlanId == plan.Id && x.Kind == "Experiment" && x.Status == "Booked"
                    ),
                    "duplicate_experiment",
                    "该计划已有待执行实验"
                );
                booking.PlanId = plan.Id;
                booking.CaseId = plan.CaseId;
            }
            if (kind == "Maintenance")
                booking.Reason = state.Text(reason);
            state.CheckSlot(booking, now);
            state.Reservations.Add(booking);
            if (kind == "Experiment")
                state.AddTask(booking.Id, "Physicist", "实验已排期，待执行并签署验证结果", now);
            return booking.Id;
        }
        private void ApplySchedule(Reservation booking, Reservation moved, string reason, DateTime now)
        {
            if (booking.Kind == "Experiment")
            {
                var plan = state.Get(state.Plans, booking.PlanId!);
                state.CurrentPlan(plan);
                state.Need(
                    state.CanExperiment(plan) && plan.MachineId == moved.ResourceId,
                    "invalid_plan",
                    "实验须使用当前待验证计划及其指定机器"
                );
                state.Need(
                    !state.Reservations.Any(x =>
                        x.Id != booking.Id
                        && x.PlanId == plan.Id
                        && x.Kind == "Experiment"
                        && x.Status == "Booked"
                    ),
                    "duplicate_experiment",
                    "该计划已有待执行实验"
                );
            }
            state.CheckSlot(moved, now);
            booking.ResourceId = moved.ResourceId;
            booking.StartUtc = moved.StartUtc;
            booking.EndUtc = moved.EndUtc;
            booking.Status = "Booked";
            booking.Reason = state.Text(reason);
            booking.Version++;
        }

        private static string ReservationRole(string kind) => kind == "Experiment" ? "Clinician" : kind == "Maintenance" ? "Physicist" : "Scheduler";

    }
}

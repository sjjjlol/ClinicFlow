using System;
using System.Linq;
using static ClinicFlow.Ois.DomainRules;
using ClinicFlow.Ois.Requests;

namespace ClinicFlow.Ois.Modules.Scheduling
{
    public sealed partial class SchedulingModule
    {

        public string RescheduleReservation(RescheduleReservationRequest request, string actor, string role, DateTime now)
        {
            Need(role == ReservationRole(Get(reservations, request.Id).Kind), "forbidden", "当前角色没有此操作权限", 403);
            var booking = Current(reservations, request.Id, request.Version);
            Need(
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
                StartUtc = Time(request.StartUtc),
                EndUtc = Time(request.EndUtc),
            };
            ApplySchedule(booking, moved, request.Reason, now);
            return booking.Id;
        }

        public string RestoreReservation(RestoreReservationRequest request, string actor, string role, DateTime now)
        {
            Need(role == ReservationRole(Get(reservations, request.Id).Kind), "forbidden", "当前角色没有此操作权限", 403);
            var booking = Current(reservations, request.Id, request.Version);
            Need(
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
            Need(role == ReservationRole(Get(reservations, request.Id).Kind), "forbidden", "当前角色没有此操作权限", 403);
            var booking = Current(reservations, request.Id, request.Version);
            Need(booking.Status == "Booked", "invalid_state", "只可处理尚未执行的预约");
            booking.Cancel(request.Reason);
            return booking.Id;
        }

        public string RecordNoShow(RecordNoShowRequest request, string actor, string role, DateTime now)
        {
            Need(role == ReservationRole(Get(reservations, request.Id).Kind), "forbidden", "当前角色没有此操作权限", 403);
            var booking = Current(reservations, request.Id, request.Version);
            Need(booking.Status == "Booked", "invalid_state", "只可处理尚未执行的预约");
            Need(
                booking.PatientId != null && now >= booking.EndUtc,
                "invalid_state",
                "预约结束后才可登记未到场"
            );
            booking.Reason = Text(request.Reason);
            booking.Status = "NoShow";
            booking.Version++;
            return booking.Id;
        }

        public string BookConsultation(BookConsultationRequest request, string actor, string role, DateTime now)
        {
            Need(role == "Scheduler", "forbidden", "当前角色没有此操作权限", 403);
            return Book(request.Kind, request.PatientId, request.CaseId, "", request.ResourceId, request.StartUtc, request.EndUtc, request.Reason, now);
        }

        public string BookTreatment(BookTreatmentRequest request, string actor, string role, DateTime now)
        {
            Need(role == "Scheduler", "forbidden", "当前角色没有此操作权限", 403);
            return Book(request.Kind, request.PatientId, request.CaseId, "", request.ResourceId, request.StartUtc, request.EndUtc, request.Reason, now);
        }

        public string BookExperiment(BookExperimentRequest request, string actor, string role, DateTime now)
        {
            Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            return Book(request.Kind, 0, "", request.PlanId, request.ResourceId, request.StartUtc, request.EndUtc, request.Reason, now);
        }

        public string BookMaintenance(BookMaintenanceRequest request, string actor, string role, DateTime now)
        {
            Need(role == "Physicist", "forbidden", "当前角色没有此操作权限", 403);
            return Book(request.Kind, 0, "", "", request.ResourceId, request.StartUtc, request.EndUtc, request.Reason, now);
        }

        private string Book(string kind, int patientId, string caseId, string planId, string resourceId, DateTimeOffset? startUtc, DateTimeOffset? endUtc, string reason, DateTime now)
        {
            Need(
                (kind == "Consultation" || kind == "Treatment" || kind == "Experiment" || kind == "Maintenance"),
                "invalid_kind",
                "预约类型无效",
                400
            );
            var booking = new Reservation
            {
                Kind = kind,
                ResourceId = resourceId,
                StartUtc = Time(startUtc),
                EndUtc = Time(endUtc),
                Reason = reason,
            };
            if (kind == "Consultation" || kind == "Treatment")
            {
                Need(
                    clinical.PatientExists(patientId),
                    "patient_missing",
                    "患者不存在",
                    404
                );
                booking.PatientId = patientId;
                if (kind == "Treatment" || caseId != "")
                {
                    var care = clinical.Case(caseId);
                    Need(
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
                var plan = planning.Plan(planId);
                planning.EnsureCurrentPlan(plan.Id);
                Need(plan.CanExperiment, "signed", "临床问题须修改处方；已通过计划不可重复验证");
                Need(plan.ReviewedBy != null, "review_required", "请先审核计划再安排实验");
                Need(
                    plan.MachineId == resourceId,
                    "machine_mismatch",
                    "实验机器须与计划一致"
                );
                Need(
                    !reservations.Any(x =>
                        x.PlanId == plan.Id && x.Kind == "Experiment" && x.Status == "Booked"
                    ),
                    "duplicate_experiment",
                    "该计划已有待执行实验"
                );
                booking.PlanId = plan.Id;
                booking.CaseId = plan.CaseId;
            }
            if (kind == "Maintenance")
                booking.Reason = Text(reason);
            CheckSlot(booking, now);
            reservations.Add(booking);
            if (kind == "Experiment")
                support.AddTask(booking.Id, "Physicist", "实验已排期，待执行并签署验证结果", now);
            return booking.Id;
        }
        private void ApplySchedule(Reservation booking, Reservation moved, string reason, DateTime now)
        {
            if (booking.Kind == "Experiment")
            {
                var plan = planning.Plan(booking.PlanId!);
                planning.EnsureCurrentPlan(plan.Id);
                Need(
                    plan.CanExperiment && plan.MachineId == moved.ResourceId,
                    "invalid_plan",
                    "实验须使用当前待验证计划及其指定机器"
                );
                Need(
                    !reservations.Any(x =>
                        x.Id != booking.Id
                        && x.PlanId == plan.Id
                        && x.Kind == "Experiment"
                        && x.Status == "Booked"
                    ),
                    "duplicate_experiment",
                    "该计划已有待执行实验"
                );
            }
            CheckSlot(moved, now);
            booking.ResourceId = moved.ResourceId;
            booking.StartUtc = moved.StartUtc;
            booking.EndUtc = moved.EndUtc;
            booking.Status = "Booked";
            booking.Reason = Text(reason);
            booking.Version++;
        }

        private static string ReservationRole(string kind) => kind == "Experiment" ? "Clinician" : kind == "Maintenance" ? "Physicist" : "Scheduler";

    }
}

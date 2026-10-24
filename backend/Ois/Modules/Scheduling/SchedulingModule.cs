using System;
using System.Collections.Generic;
using System.Linq;
using ClinicFlow.Ois.Contracts;
using ClinicFlow.Ois.Requests;
using static ClinicFlow.Ois.DomainRules;
using ClinicFlow.Ois.Modules.Support;

namespace ClinicFlow.Ois.Modules.Scheduling
{
    public sealed partial class SchedulingModule : ISchedulingContext
    {
        private readonly List<Reservation> reservations;
        private readonly List<TreatmentDelivery> deliveries;
        private readonly List<OisResource> resources;
        private readonly IClinicalContext clinical;
        private readonly IPlanningContext planning;
        private readonly SupportModule support;
        internal SchedulingModule(List<Reservation> reservations, List<TreatmentDelivery> deliveries, List<OisResource> resources,
            IClinicalContext clinical, IPlanningContext planning, SupportModule support)
        { this.reservations = reservations; this.deliveries = deliveries; this.resources = resources; this.clinical = clinical; this.planning = planning; this.support = support; }
        public void RequireMachine(string id) => Machine(id);
        public bool MachineAvailable(string id) => Machine(id).Available;
        public ExperimentSlot Experiment(string id, int version, DateTime now)
        {
            var booking = Current(reservations, id, version);
            Need(booking.Kind == "Experiment" && booking.Status == "Booked", "invalid_state", "实验不可提交结果");
            Need(now >= booking.StartUtc, "not_started", "实验预约尚未开始");
            return new ExperimentSlot(booking.Id, booking.PlanId!, booking.ResourceId);
        }
        public void CompleteExperiment(string id) => Get(reservations, id).CompleteExperiment();
        public List<string> Blockers(string reservationId)
        {
            var r = Get(reservations, reservationId);
            var reasons = new List<string>();
            var resource = Get(resources, r.ResourceId);
            if (!resource.Available) reasons.Add("机器停用：" + resource.Reason);
            if (r.Kind == "Treatment") reasons.AddRange(planning.ReleaseBlockers(r.CaseId!, r.ResourceId));
            return reasons;
        }
        internal void CheckSlot(Reservation r, DateTime now)
        {
            if (r.StartUtc.Ticks % TimeSpan.FromMinutes(15).Ticks != 0
                || r.EndUtc.Ticks % TimeSpan.FromMinutes(15).Ticks != 0
                || r.EndUtc <= r.StartUtc || r.EndUtc - r.StartUtc > TimeSpan.FromHours(4))
                throw new BusinessException("invalid_time", "起止时间必须对齐15分钟，时长为15分钟至4小时", 400);
            Need(r.StartUtc > now, "past_slot", "请选择未来时段");
            var resource = Get(resources, r.ResourceId);
            Need(
                resource.Kind == (r.Kind == "Consultation" ? "Clinician" : "Machine"),
                "invalid_resource",
                "资源类型与活动不匹配",
                400
            );
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

        public void NoActiveTreatment(string caseId) =>
            Need(
                !reservations.Any(x => x.CaseId == caseId && x.Status == "InProgress"),
                "in_progress",
                "请先结束正在执行的治疗再修订计划或处方"
            );

        internal OisResource Machine(string id)
        {
            var machine = Get(resources, id);
            Need(machine.Kind == "Machine", "invalid_resource", "请选择放疗机器", 400);
            return machine;
        }

        public void StopMachine(string id, string reason, DateTime now)
        {
            var machine = Machine(id);
            machine.Available = false;
            machine.Reason = reason;
            machine.Version++;
            foreach (
                var r in reservations.Where(x =>
                    x.ResourceId == id && (x.Status == "InProgress" || x.Status == "Booked")
                )
            )
                support.AddTask(
                    r.Id,
                    r.Status == "InProgress" ? "Clinician"
                        : r.Kind == "Experiment" ? "Clinician"
                        : "Scheduler",
                    "机器停用：" + reason,
                    now
                );
        }

        public void BlockedTasks(string caseId, string reason, DateTime now)
        {
            foreach (
                var r in reservations.Where(x =>
                    x.CaseId == caseId && x.Kind == "Treatment" && x.Status == "Booked"
                )
            )
                support.AddTask(r.Id, "Scheduler", reason, now);
        }

    }
}

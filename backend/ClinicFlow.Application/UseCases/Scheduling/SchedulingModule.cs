using System;
using System.Collections.Generic;
using System.Linq;
using ClinicFlow.Ois.Contracts;
using ClinicFlow.Application.Commands;
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
        private void CheckSlot(Reservation r, DateTime now) => SchedulePolicy.CheckSlot(r, now, resources, reservations, deliveries);

        public void NoActiveTreatment(string caseId) =>
            Need(
                !reservations.Any(x => x.CaseId == caseId && x.Status == "InProgress"),
                "in_progress",
                "请先结束正在执行的治疗再修订计划或处方"
            );

        internal OisResource Machine(string id)
        {
            var machine = Get(resources, id);
            Need(machine.Kind == "Machine", "invalid_resource", "请选择放疗机器", BusinessErrorKind.InvalidInput);
            return machine;
        }

        public void StopMachine(string id, string reason, DateTime now)
        {
            var machine = Machine(id);
            machine.SetAvailability(false, reason);
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

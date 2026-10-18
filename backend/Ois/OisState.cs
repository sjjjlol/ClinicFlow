using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicFlow.Ois
{
    // Transaction-scoped state and shared invariants. Operations run under the writer gate.
    public class OisState
    {
        public List<OisDraft> Drafts { get; set; } = new List<OisDraft>();
        public List<CareCase> Cases { get; set; } = new List<CareCase>();
        public List<Prescription> Prescriptions { get; set; } = new List<Prescription>();
        public List<TreatmentPlan> Plans { get; set; } = new List<TreatmentPlan>();
        public List<OisResource> Resources { get; set; } = new List<OisResource>();
        public List<Reservation> Reservations { get; set; } = new List<Reservation>();
        public List<TreatmentDelivery> Deliveries { get; set; } = new List<TreatmentDelivery>();
        public List<OisTask> Tasks { get; set; } = new List<OisTask>();
        public List<Patient> Patients { get; set; } = new List<Patient>();

        public List<string> Blockers(Reservation r)
        {
            var reasons = new List<string>();
            var resource = Get(Resources, r.ResourceId);
            if (!resource.Available)
                reasons.Add("机器停用：" + resource.Reason);
            if (r.Kind != "Treatment")
                return reasons;
            var care = Get(Cases, r.CaseId!);
            if (care.Terminated) reasons.Add("诊疗方案已终止");
            var plan = Plans.SingleOrDefault(x => x.Id == care.CurrentPlanId);
            if (plan == null)
            {
                reasons.Add("尚无当前治疗计划");
                return reasons;
            }
            if (!HasClinical(Get(Prescriptions, plan.PrescriptionId)) || !HasVerification(plan))
                reasons.Add("方案缺少结构化信息，需修订后重新验证");
            if (plan.PrescriptionId != care.CurrentPrescriptionId)
                reasons.Add("计划与当前处方不一致");
            if (plan.MachineId != r.ResourceId)
                reasons.Add("预约机器与计划不一致");
            if (plan.ReviewedBy == null)
                reasons.Add("待诊疗审核");
            if (plan.Validation != "Passed")
                reasons.Add(plan.Validation == "Failed" ? "物理验证未通过" : "待物理验证");
            return reasons;
        }

        internal void CheckSlot(Reservation r, DateTime now)
        {
            if (r.StartUtc.Ticks % TimeSpan.FromMinutes(15).Ticks != 0
                || r.EndUtc.Ticks % TimeSpan.FromMinutes(15).Ticks != 0
                || r.EndUtc <= r.StartUtc || r.EndUtc - r.StartUtc > TimeSpan.FromHours(4))
                throw new BusinessException("invalid_time", "起止时间必须对齐15分钟，时长为15分钟至4小时", 400);
            Need(r.StartUtc > now, "past_slot", "请选择未来时段");
            var resource = Get(Resources, r.ResourceId);
            Need(
                resource.Kind == (r.Kind == "Consultation" ? "Clinician" : "Machine"),
                "invalid_resource",
                "资源类型与活动不匹配",
                400
            );
            Need(resource.Available, "machine_unavailable", "机器已停用");
            Need(
                !Deliveries.Any(x => x.Outcome == "InProgress" && x.MachineId == r.ResourceId),
                "in_progress",
                "机器有尚未结束的治疗，请先处理"
            );
            Need(
                !Reservations.Any(x =>
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

        internal bool HasClinical(Prescription p) =>
            !string.IsNullOrWhiteSpace(p.TreatmentSite)
            && !string.IsNullOrWhiteSpace(p.TreatmentMaterials)
            && p.TotalDoseGy > 0 && p.FractionDoseGy > 0 && p.Fractions > 0;
        internal bool HasVerification(TreatmentPlan p) => new[]
        {
            p.VerificationMethod, p.Equipment, p.MeasurementConditions, p.ExpectedValues,
            p.StandardName, p.StandardVersion, p.AcceptanceCriteria, p.AllowedDeviation,
        }.All(x => !string.IsNullOrWhiteSpace(x));

        internal bool CanExperiment(TreatmentPlan p) =>
            p.Validation == "Pending" || (p.Validation == "Failed" && p.FailureType != "Clinical");





        internal void CurrentPlan(TreatmentPlan plan)
        {
            var care = Get(Cases, plan.CaseId);
            Need(!care.Terminated, "terminated", "诊疗档案已终止");
            Need(HasVerification(plan) && HasClinical(Get(Prescriptions, plan.PrescriptionId)), "details_required", "历史方案须补齐结构化信息并重新验证");
            Need(
                care.CurrentPlanId == plan.Id && care.CurrentPrescriptionId == plan.PrescriptionId,
                "superseded_plan",
                "此计划已被替代，请操作当前版本"
            );
        }

        internal void NoActiveTreatment(string caseId) =>
            Need(
                !Reservations.Any(x => x.CaseId == caseId && x.Status == "InProgress"),
                "in_progress",
                "请先结束正在执行的治疗再修订计划或处方"
            );

        internal OisResource Machine(string id)
        {
            var machine = Get(Resources, id);
            Need(machine.Kind == "Machine", "invalid_resource", "请选择放疗机器", 400);
            return machine;
        }

        internal void StopMachine(string id, string reason, DateTime now)
        {
            var machine = Machine(id);
            machine.Available = false;
            machine.Reason = reason;
            machine.Version++;
            foreach (
                var r in Reservations.Where(x =>
                    x.ResourceId == id && (x.Status == "InProgress" || x.Status == "Booked")
                )
            )
                AddTask(
                    r.Id,
                    r.Status == "InProgress" ? "Clinician"
                        : r.Kind == "Experiment" ? "Clinician"
                        : "Scheduler",
                    "机器停用：" + reason,
                    now
                );
        }

        internal void BlockedTasks(string caseId, string reason, DateTime now)
        {
            foreach (
                var r in Reservations.Where(x =>
                    x.CaseId == caseId && x.Kind == "Treatment" && x.Status == "Booked"
                )
            )
                AddTask(r.Id, "Scheduler", reason, now);
        }

        internal void AddTask(string target, string role, string reason, DateTime now) =>
            Tasks.Add(
                new OisTask
                {
                    TargetId = target,
                    Role = role,
                    Reason = reason,
                    CreatedUtc = now,
                }
            );

        internal T Get<T>(List<T> values, string id)
            where T : IOisEntity =>
            values.SingleOrDefault(x => x.Id == id)
            ?? throw new BusinessException("not_found", "记录不存在", 404);

        internal T Current<T>(List<T> values, string id, int version)
            where T : IOisEntity
        {
            var value = Get(values, id);
            Need(value.Version == version, "version_conflict", "记录已更新，请刷新后重新操作");
            return value;
        }

        internal DateTime Time(DateTimeOffset? value) =>
            value?.UtcDateTime ?? throw new BusinessException("invalid_time", "请选择起止时间", 400);

        internal string Text(string? value)
        {
            Need(
                !string.IsNullOrWhiteSpace(value) && value.Length <= 4000,
                "invalid_text",
                "请填写1至4000字符的内容或原因",
                400
            );
            return value!.Trim();
        }

        internal void Need(bool condition, string code, string message, int status = 409)
        {
            if (!condition)
                throw new BusinessException(code, message, status);
        }
    }
}

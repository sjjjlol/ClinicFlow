using System;
using System.Collections.Generic;
using System.Linq;
using ClinicFlow.Ois.Contracts;
using ClinicFlow.Ois.Requests;
using static ClinicFlow.Ois.DomainRules;
using ClinicFlow.Ois.Modules.Support;

namespace ClinicFlow.Ois.Modules.Planning
{
    public sealed partial class PlanningModule : IPlanningContext
    {
        private readonly List<TreatmentPlan> plans;
        private readonly List<PhysicalValidation> validations;
        private readonly IClinicalContext clinical;
        private readonly Func<ISchedulingContext> scheduling;
        private readonly SupportModule support;
        internal PlanningModule(List<TreatmentPlan> plans, List<PhysicalValidation> validations, IClinicalContext clinical,
            Func<ISchedulingContext> scheduling, SupportModule support)
        { this.plans = plans; this.validations = validations; this.clinical = clinical; this.scheduling = scheduling; this.support = support; }
        public PlanSummary Plan(string id)
        {
            var plan = Get(plans, id);
            return new PlanSummary(plan.Id, plan.CaseId, plan.MachineId, plan.CanExperiment(), plan.ReviewedBy);
        }
        public void EnsureCurrentPlan(string id)
        {
            var plan = Get(plans, id);
            var care = clinical.Case(plan.CaseId);
            Need(!care.Terminated, "terminated", "诊疗档案已终止");
            Need(plan.HasVerificationDetails() && clinical.HasClinical(plan.PrescriptionId), "details_required", "历史方案须补齐结构化信息并重新验证");
            Need(care.CurrentPlanId == plan.Id && care.CurrentPrescriptionId == plan.PrescriptionId,
                "superseded_plan", "此计划已被替代，请操作当前版本");
        }
        public List<string> ReleaseBlockers(string caseId, string machineId)
        {
            var reasons = new List<string>();
            var care = clinical.Case(caseId);
            if (care.Terminated) reasons.Add("诊疗方案已终止");
            var plan = plans.SingleOrDefault(x => x.Id == care.CurrentPlanId);
            if (plan == null) { reasons.Add("尚无当前治疗计划"); return reasons; }
            if (!clinical.HasClinical(plan.PrescriptionId) || !plan.HasVerificationDetails())
                reasons.Add("方案缺少结构化信息，需修订后重新验证");
            if (plan.PrescriptionId != care.CurrentPrescriptionId) reasons.Add("计划与当前处方不一致");
            if (plan.MachineId != machineId) reasons.Add("预约机器与计划不一致");
            if (plan.ReviewedBy == null) reasons.Add("待诊疗审核");
            if (plan.Validation != "Passed") reasons.Add(plan.Validation == "Failed" ? "物理验证未通过" : "待物理验证");
            return reasons;
        }
        public bool FailureResolved(string targetId)
        {
            var failed = plans.SingleOrDefault(x => x.Id == targetId && x.Validation == "Failed");
            if (failed == null) return true;
            var owner = clinical.Case(failed.CaseId);
            var current = plans.SingleOrDefault(x => x.Id == owner.CurrentPlanId);
            return owner.Terminated || (failed.FailureType == "Clinical"
                ? owner.CurrentPrescriptionId != failed.PrescriptionId : current?.Validation == "Passed");
        }
    }
}

using System;
using System.Linq;
using static ClinicFlow.Ois.DomainRules;
using ClinicFlow.Application.Commands;

namespace ClinicFlow.Ois.Modules.Planning
{
    public sealed partial class PlanningModule
    {

        public string RegisterPlan(RegisterPlanCommand request, string actor, string role, DateTime now)
        {
            Need(role == "Physicist", "forbidden", "当前角色没有此操作权限", BusinessErrorKind.Forbidden);
            var care = clinical.Case(request.CaseId, request.Version);
            scheduling().NoActiveTreatment(care.Id);
            Need(!care.Terminated, "terminated", "诊疗档案已终止");
            Need(
                care.CurrentPrescriptionId != null,
                "prescription_required",
                "请先签署处方"
            );
            Need(!plans.Any(x => x.PrescriptionId == care.CurrentPrescriptionId && x.Validation == "Failed" && x.FailureType == "Clinical"), "clinical_revision_required", "临床问题须先由诊疗人员修订处方或终止");
            Need(clinical.HasClinical(care.CurrentPrescriptionId!), "clinical_details_required", "历史处方缺少结构化信息，请先签署完整新处方");
            var protocol = new VerificationProtocol(request.VerificationMethod, request.Equipment,
                request.MeasurementConditions, request.ExpectedValues, request.StandardName, request.StandardVersion,
                request.AcceptanceCriteria, request.AllowedDeviation);
            scheduling().RequireMachine(request.ResourceId);
            var plan = TreatmentPlan.Register(care.Id, care.CurrentPrescriptionId!, plans.Count(x => x.CaseId == care.Id) + 1,
                protocol, request.Text, request.Attachment, request.ResourceId, actor, now);
            if (care.CurrentPlanId != null)
                Text(request.Reason);
            plans.Add(plan);
            support.RemoveDraft(request.CaseId, actor, "plan");
            return plan.Id;
        }

        public string ReviewPlan(ReviewPlanCommand request, string actor, string role, DateTime now)
        {
            Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", BusinessErrorKind.Forbidden);
            var plan = Current(plans, request.Id, request.Version);
            EnsureCurrentPlan(plan.Id);
            plan.Review(actor, now);
            return plan.Id;
        }

        public string SavePlanDraft(SavePlanDraftCommand request, string actor, string role, DateTime now)
        {
            Need(role == "Physicist", "forbidden", "当前角色没有此操作权限", BusinessErrorKind.Forbidden);
            return support.Save(request.CaseId, request.Version, request.DraftVersion, request.DraftJson, request.Kind, actor);
        }

        public string CorrectPlan(CorrectPlanCommand request, string actor, string role, DateTime now)
        {
            Need(role == "Physicist", "forbidden", "当前角色没有此操作权限", BusinessErrorKind.Forbidden);
            Text(request.Text);
            Text(request.Reason);
            // Corrections are appended in OisAudit. They never silently change a signed result or release a plan.
            var plan = Current(plans, request.Id, request.Version);
            Need(plan.ValidatedBy != null, "not_signed", "本计划尚无签署验证记录");
            support.AddTask(plan.Id, "Physicist", "验证更正待修订计划：" + request.Text, now);
            var care = clinical.Case(plan.CaseId);
            if (care.CurrentPlanId == plan.Id)
            {
                scheduling().NoActiveTreatment(care.Id);
                clinical.SelectPlan(care.Id, null);
                scheduling().BlockedTasks(care.Id, "验证更正，待新版验证", now);
            }
            return request.Id;
        }

    }
}

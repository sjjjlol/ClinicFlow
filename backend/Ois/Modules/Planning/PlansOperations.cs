using System;
using System.Linq;
using static ClinicFlow.Ois.DomainRules;
using ClinicFlow.Ois.Requests;

namespace ClinicFlow.Ois.Modules.Planning
{
    public sealed partial class PlanningModule
    {

        public string RegisterPlan(RegisterPlanRequest request, string actor, string role, DateTime now)
        {
            Need(role == "Physicist", "forbidden", "当前角色没有此操作权限", 403);
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
            ValidateVerification(request);
            scheduling().RequireMachine(request.ResourceId);
            Need(request.Attachment.Length <= 1000, "invalid_text", "附件引用过长", 400);
            var plan = new TreatmentPlan
            {
                CaseId = care.Id,
                PrescriptionId = care.CurrentPrescriptionId!,
                Revision = plans.Count(x => x.CaseId == care.Id) + 1,
                VerificationMethod = request.VerificationMethod,
                Equipment = request.Equipment,
                MeasurementConditions = request.MeasurementConditions,
                ExpectedValues = request.ExpectedValues,
                StandardName = request.StandardName,
                StandardVersion = request.StandardVersion,
                AcceptanceCriteria = request.AcceptanceCriteria,
                AllowedDeviation = request.AllowedDeviation,
                Summary = Text(request.Text),
                Attachment = request.Attachment,
                MachineId = request.ResourceId,
                RegisteredBy = actor,
                RegisteredUtc = now,
            };
            if (care.CurrentPlanId != null)
                Text(request.Reason);
            plans.Add(plan);
            clinical.SelectPlan(care.Id, plan.Id);
            support.AddTask(plan.Id, "Clinician", "新计划待审核及实验排期", now);
            scheduling().BlockedTasks(care.Id, "计划更新，待重新审核与验证", now);
            support.RemoveDraft(request.CaseId, actor, "plan");
            return plan.Id;
        }

        public string ReviewPlan(ReviewPlanRequest request, string actor, string role, DateTime now)
        {
            Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            var plan = Current(plans, request.Id, request.Version);
            EnsureCurrentPlan(plan.Id);
            plan.Review(actor, now);
            return plan.Id;
        }

        public string SavePlanDraft(SavePlanDraftRequest request, string actor, string role, DateTime now)
        {
            Need(role == "Physicist", "forbidden", "当前角色没有此操作权限", 403);
            return support.Save(request.CaseId, request.Version, request.DraftVersion, request.DraftJson, request.Kind, actor);
        }

        public string CorrectPlan(CorrectPlanRequest request, string actor, string role, DateTime now)
        {
            Need(role == "Physicist", "forbidden", "当前角色没有此操作权限", 403);
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

        private void ValidateVerification(RegisterPlanRequest request)
        {
            Text(request.VerificationMethod);
            Text(request.Equipment);
            Text(request.MeasurementConditions);
            Text(request.ExpectedValues);
            Text(request.StandardName);
            Text(request.StandardVersion);
            Text(request.AcceptanceCriteria);
            Text(request.AllowedDeviation);
        }
    }
}

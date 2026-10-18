using System;
using System.Linq;
using ClinicFlow.Ois.Requests;

namespace ClinicFlow.Ois.Operations
{
    public sealed class PlansOperations
    {
        private readonly OisState state;
        public PlansOperations(OisState state) => this.state = state;

        public string RegisterPlan(RegisterPlanRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == "Physicist", "forbidden", "当前角色没有此操作权限", 403);
            var care = state.Current(state.Cases, request.CaseId, request.Version);
            state.NoActiveTreatment(care.Id);
            state.Need(!care.Terminated, "terminated", "诊疗档案已终止");
            state.Need(
                care.CurrentPrescriptionId != null,
                "prescription_required",
                "请先签署处方"
            );
            state.Need(!state.Plans.Any(x => x.PrescriptionId == care.CurrentPrescriptionId && x.Validation == "Failed" && x.FailureType == "Clinical"), "clinical_revision_required", "临床问题须先由诊疗人员修订处方或终止");
            state.Need(state.HasClinical(state.Get(state.Prescriptions, care.CurrentPrescriptionId!)), "clinical_details_required", "历史处方缺少结构化信息，请先签署完整新处方");
            ValidateVerification(request);
            state.Machine(request.ResourceId);
            state.Need(request.Attachment.Length <= 1000, "invalid_text", "附件引用过长", 400);
            var plan = new TreatmentPlan
            {
                CaseId = care.Id,
                PrescriptionId = care.CurrentPrescriptionId!,
                Revision = state.Plans.Count(x => x.CaseId == care.Id) + 1,
                VerificationMethod = request.VerificationMethod,
                Equipment = request.Equipment,
                MeasurementConditions = request.MeasurementConditions,
                ExpectedValues = request.ExpectedValues,
                StandardName = request.StandardName,
                StandardVersion = request.StandardVersion,
                AcceptanceCriteria = request.AcceptanceCriteria,
                AllowedDeviation = request.AllowedDeviation,
                Summary = state.Text(request.Text),
                Attachment = request.Attachment,
                MachineId = request.ResourceId,
                RegisteredBy = actor,
                RegisteredUtc = now,
            };
            if (care.CurrentPlanId != null)
                state.Text(request.Reason);
            state.Plans.Add(plan);
            care.CurrentPlanId = plan.Id;
            care.Version++;
            state.AddTask(plan.Id, "Clinician", "新计划待审核及实验排期", now);
            state.BlockedTasks(care.Id, "计划更新，待重新审核与验证", now);
            state.Drafts.RemoveAll(x => x.CaseId == request.CaseId && x.Actor == actor && x.Operation == "plan");
            return plan.Id;
        }

        public string ReviewPlan(ReviewPlanRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            var plan = state.Current(state.Plans, request.Id, request.Version);
            state.CurrentPlan(plan);
            state.Need(plan.ReviewedBy == null, "signed", "本版本已审核；修改请创建新版本");
            state.Need(plan.Validation != "Failed", "failed_plan", "验证失败的计划须修订为新版本");
            plan.ReviewedBy = actor;
            plan.ReviewedUtc = now;
            plan.Version++;
            return plan.Id;
        }

        public string SavePlanDraft(SavePlanDraftRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == "Physicist", "forbidden", "当前角色没有此操作权限", 403);
            return new DraftOperations(state).Save(request.CaseId, request.Version, request.DraftVersion, request.DraftJson, request.Kind, actor);
        }

        public string CorrectPlan(CorrectPlanRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == "Physicist", "forbidden", "当前角色没有此操作权限", 403);
            state.Text(request.Text);
            state.Text(request.Reason);
            // Corrections are appended in OisAudit. They never silently change a signed result or release a plan.
            var plan = state.Current(state.Plans, request.Id, request.Version);
            state.Need(plan.ValidatedBy != null, "not_signed", "本计划尚无签署验证记录");
            state.AddTask(plan.Id, "Physicist", "验证更正待修订计划：" + request.Text, now);
            var care = state.Get(state.Cases, plan.CaseId);
            if (care.CurrentPlanId == plan.Id)
            {
                state.NoActiveTreatment(care.Id);
                care.CurrentPlanId = null;
                care.Version++;
                state.BlockedTasks(care.Id, "验证更正，待新版验证", now);
            }
            return request.Id;
        }

        private void ValidateVerification(RegisterPlanRequest request)
        {
            state.Text(request.VerificationMethod);
            state.Text(request.Equipment);
            state.Text(request.MeasurementConditions);
            state.Text(request.ExpectedValues);
            state.Text(request.StandardName);
            state.Text(request.StandardVersion);
            state.Text(request.AcceptanceCriteria);
            state.Text(request.AllowedDeviation);
        }
    }
}

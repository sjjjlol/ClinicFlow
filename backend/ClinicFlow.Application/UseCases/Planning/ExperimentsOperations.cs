using System;
using System.Linq;
using static ClinicFlow.Ois.DomainRules;
using ClinicFlow.Application.Commands;

namespace ClinicFlow.Ois.Modules.Planning
{
    public sealed partial class PlanningModule
    {

        public string SubmitExperimentResult(SubmitExperimentResultCommand request, string actor, string role, DateTime now)
        {
            Need(role == "Physicist", "forbidden", "当前角色没有此操作权限", BusinessErrorKind.Forbidden);
            var booking = scheduling().Experiment(request.Id, request.Version, now);
            var plan = Get(plans, booking.PlanId!);
            EnsureCurrentPlan(plan.Id);
            Need(plan.CanExperiment(), "signed", "本版本不可验证");
            Need(plan.ReviewedBy != null, "review_required", "计划尚未审核");
            Text(request.MeasuredValues);
            Text(request.ResultAttachment);
            Need(
                (request.Outcome == "Passed" || request.Outcome == "Failed"),
                "invalid_result",
                "请选择通过或不通过", BusinessErrorKind.InvalidInput);
            Need(scheduling().MachineAvailable(booking.ResourceId), "machine_unavailable", "机器已停用");
            Text(request.Text);
            if (request.Outcome == "Failed")
                Need(
                    (request.FailureType == "Plan" || request.FailureType == "Clinical" || request.FailureType == "Equipment"),
                    "invalid_failure",
                    "请选择失败类型", BusinessErrorKind.InvalidInput);
            var result = PhysicalValidation.Sign(booking.Id, request.Outcome, request.Text,
                request.FailureType, request.MeasuredValues, request.ResultAttachment, actor, now);
            plan.RecordValidation(result);
            validations.Add(result);
            return plan.Id;
        }

    }
}

using System;
using System.Linq;
using ClinicFlow.Ois.Requests;

namespace ClinicFlow.Ois.Operations
{
    public sealed class ExperimentsOperations
    {
        private readonly OisState state;
        public ExperimentsOperations(OisState state) => this.state = state;

        public string SubmitExperimentResult(SubmitExperimentResultRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == "Physicist", "forbidden", "当前角色没有此操作权限", 403);
            var booking = state.Current(state.Reservations, request.Id, request.Version);
            state.Need(
                booking.Kind == "Experiment" && booking.Status == "Booked",
                "invalid_state",
                "实验不可提交结果"
            );
            state.Need(now >= booking.StartUtc, "not_started", "实验预约尚未开始");
            var plan = state.Get(state.Plans, booking.PlanId!);
            state.CurrentPlan(plan);
            state.Need(state.CanExperiment(plan), "signed", "本版本不可验证");
            state.Need(plan.ReviewedBy != null, "review_required", "计划尚未审核");
            state.Text(request.MeasuredValues);
            state.Text(request.ResultAttachment);
            state.Need(
                (request.Outcome == "Passed" || request.Outcome == "Failed"),
                "invalid_result",
                "请选择通过或不通过",
                400
            );
            state.Need(state.Machine(booking.ResourceId).Available, "machine_unavailable", "机器已停用");
            state.Text(request.Text);
            if (request.Outcome == "Failed")
                state.Need(
                    (request.FailureType == "Plan" || request.FailureType == "Clinical" || request.FailureType == "Equipment"),
                    "invalid_failure",
                    "请选择失败类型",
                    400
                );
            plan.Validation = request.Outcome;
            plan.ValidatedBy = actor;
            plan.ValidatedUtc = now;
            plan.ValidationNote = request.Text;
            plan.FailureType = request.Outcome == "Failed" ? request.FailureType : "";
            plan.Version++;
            booking.ValidationOutcome = request.Outcome;
            booking.ValidationNote = request.Text;
            booking.FailureType = plan.FailureType;
            booking.MeasuredValues = request.MeasuredValues;
            booking.ResultAttachment = request.ResultAttachment;
            booking.SignedBy = actor;
            booking.SignedUtc = now;
            booking.Status = "Completed";
            booking.Version++;
            if (request.Outcome == "Failed")
            {
                state.AddTask(
                    plan.Id,
                    request.FailureType == "Clinical" ? "Clinician" : "Physicist",
                    request.Text,
                    now
                );
                if (request.FailureType != "Clinical")
                    state.AddTask(plan.Id, "Clinician", "物理验证失败，物理师处理中：" + request.Text, now);
                state.BlockedTasks(plan.CaseId, "物理验证失败：" + request.Text, now);
                if (request.FailureType == "Equipment")
                    state.StopMachine(booking.ResourceId, request.Text, now);
            }
            return plan.Id;
        }

    }
}

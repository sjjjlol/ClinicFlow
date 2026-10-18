using System;
using System.Linq;
using ClinicFlow.Ois.Requests;

namespace ClinicFlow.Ois.Operations
{
    public sealed class TasksOperations
    {
        private readonly OisState state;
        public TasksOperations(OisState state) => this.state = state;

        public string ReadTask(ReadTaskRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == state.Get(state.Tasks, request.Id).Role, "forbidden", "当前角色没有此操作权限", 403);
            var task = state.Current(state.Tasks, request.Id, request.Version);
            state.Need(task.ResolvedBy == null, "invalid_state", "待办已经处理");
            task.ReadBy = actor;
            task.Version++;
            return task.Id;
        }

        public string ResolveTask(ResolveTaskRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == state.Get(state.Tasks, request.Id).Role, "forbidden", "当前角色没有此操作权限", 403);
            var task = state.Current(state.Tasks, request.Id, request.Version);
            state.Need(task.ResolvedBy == null, "invalid_state", "待办已经处理");
            task.ReadBy = actor;
            var failed = state.Plans.SingleOrDefault(x => x.Id == task.TargetId && x.Validation == "Failed");
            if (failed != null)
            {
                var owner = state.Get(state.Cases, failed.CaseId);
                var current = state.Plans.SingleOrDefault(x => x.Id == owner.CurrentPlanId);
                state.Need(owner.Terminated || (failed.FailureType == "Clinical"
                    ? owner.CurrentPrescriptionId != failed.PrescriptionId
                    : current?.Validation == "Passed"), "unresolved_failure", "须先完成修订、重测通过或终止，已读不等于已解决");
            }
            task.Resolution = state.Text(request.Text);
            task.ResolvedBy = actor;
            task.ResolvedUtc = now;
            task.Version++;
            return task.Id;
        }

    }
}

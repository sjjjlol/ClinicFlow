using System;
using System.Linq;
using static ClinicFlow.Ois.DomainRules;
using ClinicFlow.Application.Commands;

namespace ClinicFlow.Ois.Modules.Support
{
    public sealed partial class SupportModule
    {

        public string ReadTask(ReadTaskCommand request, string actor, string role, DateTime now)
        {
            Need(role == Get(tasks, request.Id).Role, "forbidden", "当前角色没有此操作权限", BusinessErrorKind.Forbidden);
            var task = Current(tasks, request.Id, request.Version);
            Need(task.ResolvedBy == null, "invalid_state", "待办已经处理");
            task.Read(actor);
            return task.Id;
        }

        public string ResolveTask(ResolveTaskCommand request, string actor, string role, DateTime now)
        {
            Need(role == Get(tasks, request.Id).Role, "forbidden", "当前角色没有此操作权限", BusinessErrorKind.Forbidden);
            var task = Current(tasks, request.Id, request.Version);
            Need(task.ResolvedBy == null, "invalid_state", "待办已经处理");
            Need(planning().FailureResolved(task.TargetId), "unresolved_failure", "须先完成修订、重测通过或终止，已读不等于已解决");
            task.Resolve(actor, request.Text, now);
            return task.Id;
        }

    }
}

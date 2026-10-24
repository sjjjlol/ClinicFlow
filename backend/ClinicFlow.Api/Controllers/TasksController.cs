using System.Threading;
using System.Threading.Tasks;
using ClinicFlow.Ois;
using ClinicFlow.Ois.Requests;
using Microsoft.AspNetCore.Mvc;

namespace ClinicFlow.Controllers
{
    public sealed class TasksController : OisCommandController
    {
        public TasksController(CommandExecutor executor) : base(executor) { }

        [HttpPost("tasks/{id}/read")]
        [Idempotent]
        public Task<IActionResult> ReadTask(string id, [FromBody] ReadTaskRequest input, CancellationToken ct)
        {
            input.Id = id;
            return Run(input.ToCommand(), (w, c, actor, role, now) => w.Support.ReadTask(c, actor, role, now), ct);
        }

        [HttpPost("tasks/{id}/resolve")]
        [Idempotent]
        public Task<IActionResult> ResolveTask(string id, [FromBody] ResolveTaskRequest input, CancellationToken ct)
        {
            input.Id = id;
            return Run(input.ToCommand(), (w, c, actor, role, now) => w.Support.ResolveTask(c, actor, role, now), ct);
        }
    }
}

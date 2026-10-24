using System.Threading;
using System.Threading.Tasks;
using ClinicFlow.Ois;
using ClinicFlow.Ois.Requests;
using Microsoft.AspNetCore.Mvc;

namespace ClinicFlow.Controllers
{
    public sealed class PlansController : OisCommandController
    {
        public PlansController(CommandExecutor executor) : base(executor) { }

        [HttpPost("cases/{id}/plans")]
        [Idempotent]
        public Task<IActionResult> RegisterPlan(string id, [FromBody] RegisterPlanRequest input, CancellationToken ct)
        {
            input.CaseId = id;
            return Run(input.ToCommand(), (w, c, actor, role, now) => w.Planning.RegisterPlan(c, actor, role, now), ct);
        }

        [HttpPost("plans/{id}/review")]
        [Idempotent]
        public Task<IActionResult> ReviewPlan(string id, [FromBody] ReviewPlanRequest input, CancellationToken ct)
        {
            input.Id = id;
            return Run(input.ToCommand(), (w, c, actor, role, now) => w.Planning.ReviewPlan(c, actor, role, now), ct);
        }

        [HttpPut("cases/{id}/plan-draft")]
        [Idempotent]
        public Task<IActionResult> SavePlanDraft(string id, [FromBody] SavePlanDraftRequest input, CancellationToken ct)
        {
            input.CaseId = id;
            return Run(input.ToCommand(), (w, c, actor, role, now) => w.Planning.SavePlanDraft(c, actor, role, now), ct);
        }

        [HttpPost("plans/{id}/corrections")]
        [Idempotent]
        public Task<IActionResult> CorrectPlan(string id, [FromBody] CorrectPlanRequest input, CancellationToken ct)
        {
            input.Id = id;
            return Run(input.ToCommand(), (w, c, actor, role, now) => w.Planning.CorrectPlan(c, actor, role, now), ct);
        }
    }
}

using System.Threading;
using System.Threading.Tasks;
using ClinicFlow.Ois;
using ClinicFlow.Ois.Requests;
using Microsoft.AspNetCore.Mvc;

namespace ClinicFlow.Controllers
{
    public sealed class CasesController : OisCommandController
    {
        public CasesController(CommandExecutor executor) : base(executor) { }

        [HttpPost("cases")]
        [Idempotent]
        public Task<IActionResult> CreateCase([FromBody] CreateCaseRequest input, CancellationToken ct)
        {
            return Run(input.ToCommand(), (w, c, actor, role, now) => w.Clinical.CreateCase(c, actor, role, now), ct);
        }

        [HttpPost("cases/{id}/terminate")]
        [Idempotent]
        public Task<IActionResult> TerminateCase(string id, [FromBody] TerminateCaseRequest input, CancellationToken ct)
        {
            input.Id = id;
            return Run(input.ToCommand(), (w, c, actor, role, now) => w.Clinical.TerminateCase(c, actor, role, now), ct);
        }

        [HttpPost("cases/{id}/notes")]
        [Idempotent]
        public Task<IActionResult> AddCaseNote(string id, [FromBody] AddCaseNoteRequest input, CancellationToken ct)
        {
            input.Id = id;
            return Run(input.ToCommand(), (w, c, actor, role, now) => w.Clinical.AddCaseNote(c, actor, role, now), ct);
        }
    }
}

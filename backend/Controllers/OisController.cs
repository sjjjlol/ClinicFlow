using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ClinicFlow.Ois;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicFlow.Controllers
{
    [ApiController]
    [Authorize(Policy = "ois")]
    [Route("api/ois")]
    public sealed class OisController : ControllerBase
    {
        private readonly OisService service;

        public OisController(OisService service)
        {
            this.service = service;
        }

        [HttpGet("workspace")]
        public async Task<IActionResult> Workspace(CancellationToken ct)
        {
            object snapshot = await service.Snapshot(User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct);
            return Ok(snapshot);
        }

        [HttpPost("commands/case")]
        public Task<IActionResult> CreateCase([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("case", input, ct);
        }

        [HttpPost("commands/prescribe")]
        public Task<IActionResult> Prescribe([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("prescribe", input, ct);
        }

        [HttpPost("commands/plan")]
        public Task<IActionResult> RegisterPlan([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("plan", input, ct);
        }

        [HttpPost("commands/review")]
        public Task<IActionResult> ReviewPlan([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("review", input, ct);
        }

        [HttpPost("commands/book")]
        public Task<IActionResult> Book([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("book", input, ct);
        }

        [HttpPost("commands/reschedule")]
        public Task<IActionResult> Reschedule([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("reschedule", input, ct);
        }

        [HttpPost("commands/restore")]
        public Task<IActionResult> Restore([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("restore", input, ct);
        }

        [HttpPost("commands/cancel")]
        public Task<IActionResult> Cancel([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("cancel", input, ct);
        }

        [HttpPost("commands/no-show")]
        public Task<IActionResult> RecordNoShow([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("no-show", input, ct);
        }

        [HttpPost("commands/validate")]
        public Task<IActionResult> ValidatePlan([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("validate", input, ct);
        }

        [HttpPost("commands/machine")]
        public Task<IActionResult> SetMachineAvailability([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("machine", input, ct);
        }

        [HttpPost("commands/consult")]
        public Task<IActionResult> CompleteConsultation([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("consult", input, ct);
        }

        [HttpPost("commands/start")]
        public Task<IActionResult> StartTreatment([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("start", input, ct);
        }

        [HttpPost("commands/finish")]
        public Task<IActionResult> FinishTreatment([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("finish", input, ct);
        }

        [HttpPost("commands/not-executed")]
        public Task<IActionResult> RecordNotExecuted([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("not-executed", input, ct);
        }

        [HttpPost("commands/read-task")]
        public Task<IActionResult> ReadTask([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("read-task", input, ct);
        }

        [HttpPost("commands/resolve-task")]
        public Task<IActionResult> ResolveTask([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("resolve-task", input, ct);
        }

        [HttpPost("commands/correction")]
        public Task<IActionResult> CorrectRecord([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("correction", input, ct);
        }

        [HttpPost("commands/save-draft")]
        public Task<IActionResult> SaveDraft([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("save-draft", input, ct);
        }

        [HttpPost("commands/terminate")]
        public Task<IActionResult> TerminateCase([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("terminate", input, ct);
        }

        [HttpPost("commands/note")]
        public Task<IActionResult> AddNote([FromBody] OisCommand input, CancellationToken ct)
        {
            return ExecuteCommand("note", input, ct);
        }

        [HttpPost("commands/{operation}", Order = 1)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public IActionResult UnknownCommand()
        {
            throw new BusinessException("invalid_operation", "未知 OIS 操作", 400);
        }

        private async Task<IActionResult> ExecuteCommand(string operation, OisCommand input, CancellationToken ct)
        {
            object result = await service.Execute(
                operation,
                input,
                User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                User.FindFirstValue(ClaimTypes.Role)!,
                Request.Headers["Idempotency-Key"].ToString(),
                HttpContext.TraceIdentifier,
                ct);
            return Ok(result);
        }
    }
}

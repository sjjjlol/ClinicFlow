using System.Security.Claims;
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

        [HttpPost("commands/{operation}")]
        public async Task<IActionResult> Execute([FromRoute] string operation, [FromBody] OisCommand input, CancellationToken ct)
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

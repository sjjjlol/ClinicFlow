using System;
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
    public abstract class OisCommandController : ControllerBase
    {
        private readonly CommandExecutor executor;
        protected OisCommandController(CommandExecutor executor) => this.executor = executor;

        protected async Task<IActionResult> Run<T>(T input,
            Func<OisSession, T, string, string, DateTime, string> handler, CancellationToken ct) where T : class
        {
            return Ok(await executor.Execute(input,
                User.FindFirstValue(ClaimTypes.NameIdentifier)!, User.FindFirstValue(ClaimTypes.Role)!,
                Request.Headers["Idempotency-Key"].ToString(), HttpContext.TraceIdentifier, ct, handler));
        }
    }
}

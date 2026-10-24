using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Threading;

namespace ClinicFlow.Controllers
{
    [ApiController]
    [Route("api/health")]
    public sealed class HealthController : ControllerBase
    {
        private readonly IDatabaseHealth db;

        public HealthController(IDatabaseHealth db)
        {
            this.db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            return Ok(new { status = await db.CanConnect(ct) ? "ready" : "unavailable" });
        }
    }
}

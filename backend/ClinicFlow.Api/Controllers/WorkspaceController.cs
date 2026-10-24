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
    [Route("api/ois/workspace")]
    public sealed class WorkspaceController : ControllerBase
    {
        private readonly IWorkspaceReader query;
        public WorkspaceController(IWorkspaceReader query) => this.query = query;
        [HttpGet]
        public Task<WorkspaceResponse> Get(CancellationToken ct) =>
            query.Snapshot(User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct);
    }
}

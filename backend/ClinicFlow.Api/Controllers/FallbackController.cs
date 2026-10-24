using Microsoft.AspNetCore.Mvc;

namespace ClinicFlow.Controllers
{
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    public sealed class FallbackController : ControllerBase
    {
        [Route("api/{**path}", Order = int.MaxValue - 1)]
        [AcceptVerbs("GET", "POST", "PUT", "DELETE", "PATCH")]
        public IActionResult ApiNotFound()
        {
            return NotFound(new { code = "not_found" });
        }

        [Route("{**path:nonfile}", Order = int.MaxValue)]
        public IActionResult PageNotFound()
        {
            return NotFound(new { code = "not_found" });
        }
    }
}

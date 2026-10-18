using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ClinicFlow.Contracts;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClinicFlow.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public sealed class AuthController : ControllerBase
    {
        private readonly ClinicDb db;
        private readonly IAntiforgery antiforgery;

        public AuthController(ClinicDb db, IAntiforgery antiforgery)
        {
            this.db = db;
            this.antiforgery = antiforgery;
        }

        [HttpGet("csrf")]
        public IActionResult Csrf()
        {
            return Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
        }

        [HttpPost("login")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest input, CancellationToken ct)
        {
            DemoUser? user = await db.Users.FindAsync(
                new object[] { (input.Username ?? "").Trim().ToLowerInvariant() }, ct);
            if (user == null
                || !Identity.IsStaffRole(user.Role)
                || new PasswordHasher<DemoUser>().VerifyHashedPassword(user, user.PasswordHash, input.Password ?? "")
                    == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new { code = "invalid_credentials", message = "账号或密码不正确" });
            }

            await Identity.SignIn(user, HttpContext);
            return Ok(new { name = user.Id, role = user.Role });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return NoContent();
        }

        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            return Ok(new { name = User.Identity!.Name, role = User.FindFirstValue(ClaimTypes.Role) });
        }
    }
}

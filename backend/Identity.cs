using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClinicFlow;

public static class Identity
{
    public static async Task Seed(ClinicDb db, IConfiguration config)
    {
        if (await db.Users.AnyAsync())
            return;
        var password =
            config["DEMO_PASSWORD"]
            ?? throw new InvalidOperationException("DEMO_PASSWORD required for initial seed");
        if (password.Length < 12)
            throw new InvalidOperationException("Demo password requires 12+ characters");
        foreach (var role in new[] { "Scheduler", "TaskOperator", "Admin" })
        {
            var user = new DemoUser { Id = role.ToLowerInvariant(), Role = role };
            user.PasswordHash = new PasswordHasher<DemoUser>().HashPassword(user, password);
            db.Users.Add(user);
        }
        await db.SaveChangesAsync();
    }

    public static void MapIdentity(this WebApplication app)
    {
        app.MapGet(
            "/api/auth/csrf",
            (HttpContext ctx, IAntiforgery anti) =>
                new { token = anti.GetAndStoreTokens(ctx).RequestToken }
        );
        app.MapPost(
                "/api/auth/login",
                async (Login input, ClinicDb db, HttpContext ctx, CancellationToken ct) =>
                {
                    var user = await db.Users.FindAsync([input.Username ?? ""], ct);
                    if (
                        user is null
                        || new PasswordHasher<DemoUser>().VerifyHashedPassword(
                            user,
                            user.PasswordHash,
                            input.Password ?? ""
                        ) == PasswordVerificationResult.Failed
                    )
                        return Results.Json(
                            new { code = "invalid_credentials", message = "账号或密码不正确" },
                            statusCode: 401
                        );
                    var identity = new ClaimsIdentity(
                        [
                            new(ClaimTypes.NameIdentifier, user.Id),
                            new(ClaimTypes.Name, user.Id),
                            new(ClaimTypes.Role, user.Role),
                        ],
                        CookieAuthenticationDefaults.AuthenticationScheme
                    );
                    await ctx.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(identity)
                    );
                    return Results.Ok(new { name = user.Id, role = user.Role });
                }
            )
            .RequireRateLimiting("login");
        app.MapPost(
                "/api/auth/logout",
                async (HttpContext ctx) =>
                {
                    await ctx.SignOutAsync();
                    return Results.NoContent();
                }
            )
            .RequireAuthorization();
        app.MapGet(
                "/api/auth/me",
                (HttpContext ctx) =>
                    new
                    {
                        name = ctx.User.Identity!.Name,
                        role = ctx.User.FindFirstValue(ClaimTypes.Role),
                    }
            )
            .RequireAuthorization();
    }

    public record Login(string Username, string Password);
}

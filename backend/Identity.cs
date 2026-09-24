using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClinicFlow;

// ===== 演示用身份认证：Cookie 会话 + 角色 claims（对照 Spring Security 表单登录）=====
public static class Identity
{
    // Seed：首次启动写入三个演示账号（每角色一个），密码取自环境变量 DEMO_PASSWORD。
    public static async Task Seed(ClinicDb db, IConfiguration config)
    {
        if (await db.Users.AnyAsync()) // AnyAsync() 无参版 ≈ "表非空？"
            return;
        var password =
            config["DEMO_PASSWORD"]
            ?? throw new InvalidOperationException("DEMO_PASSWORD required for initial seed");
        if (password.Length < 12)
            throw new InvalidOperationException("Demo password requires 12+ characters");
        // new[] { ... }：隐式类型数组（≈ Java new String[]{...}）。
        foreach (var role in new[] { "Scheduler", "TaskOperator", "Admin" })
        {
            // ToLowerInvariant ≈ toLowerCase(Locale.ROOT)。
            var user = new DemoUser { Id = role.ToLowerInvariant(), Role = role };
            // PasswordHasher<T>：ASP.NET Identity 的 PBKDF2 密码哈希（≈ Spring 的 BCryptPasswordEncoder）。
            user.PasswordHash = new PasswordHasher<DemoUser>().HashPassword(user, password);
            db.Users.Add(user);
        }
        await db.SaveChangesAsync();
    }

    public static void MapIdentity(this WebApplication app)
    {
        // 发放 CSRF 令牌：前端先 GET 它，之后写操作在 X-CSRF-TOKEN 头回传。
        // (HttpContext ctx, IAntiforgery anti)：HttpContext 由框架传入，IAntiforgery 由 DI 注入。
        app.MapGet(
            "/api/auth/csrf",
            (HttpContext ctx, IAntiforgery anti) =>
                new { token = anti.GetAndStoreTokens(ctx).RequestToken }
        );
        app.MapPost(
                "/api/auth/login",
                async (Login input, ClinicDb db, HttpContext ctx, CancellationToken ct) =>
                {
                    // FindAsync([主键])：按主键查询，[...] 是 object[] 集合表达式（复合主键时放多个值）。
                    var user = await db.Users.FindAsync([input.Username ?? ""], ct);
                    if (
                        user is null
                        || new PasswordHasher<DemoUser>().VerifyHashedPassword(
                            user,
                            user.PasswordHash,
                            input.Password ?? ""
                        ) == PasswordVerificationResult.Failed // 枚举比较（≈ Java enum ==）
                    )
                        // Results.Json(..., statusCode: 401)：显式状态码的 JSON 响应；
                        // statusCode: 是命名参数（Java 没有，≈ 更清晰的参数传递）。
                        return Results.Json(
                            new { code = "invalid_credentials", message = "账号或密码不正确" },
                            statusCode: 401
                        );
                    // ClaimsIdentity：一组 claim（键值断言）≈ UserDetails 的 username + authorities。
                    var identity = new ClaimsIdentity(
                        [
                            new(ClaimTypes.NameIdentifier, user.Id),
                            new(ClaimTypes.Name, user.Id),
                            new(ClaimTypes.Role, user.Role),
                        ],
                        CookieAuthenticationDefaults.AuthenticationScheme
                    );
                    // SignInAsync：写入加密的会话 Cookie（≈ Spring Security 认证成功后建 SecurityContext + Session）。
                    await ctx.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(identity)
                    );
                    return Results.Ok(new { name = user.Id, role = user.Role });
                }
            )
            // 端点限流策略（Program.cs 注册的 "login" 固定窗口）。
            .RequireRateLimiting("login");
        app.MapPost(
                "/api/auth/logout",
                async (HttpContext ctx) =>
                {
                    await ctx.SignOutAsync(); // 清除会话 Cookie
                    return Results.NoContent(); // 204 ≈ ResponseEntity.noContent()
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

    // record DTO：登录请求体。JSON {username, password} 自动绑定（Web 默认 camelCase 匹配）。
    public record Login(string Username, string Password);
}

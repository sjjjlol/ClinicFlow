using System.Security.Claims;
using System.Text.RegularExpressions;
using ClinicFlow.Scheduling;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

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
        if (password.Length < 8)
            throw new InvalidOperationException("Demo password requires 8+ characters");
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
                "/api/auth/register",
                async (Registration input, ClinicDb db, HttpContext ctx, CancellationToken ct) =>
                {
                    if (ctx.User.Identity?.IsAuthenticated == true)
                        throw new BusinessException(
                            "already_signed_in",
                            "请先退出当前账号再注册",
                            409
                        );
                    var user = await Register(input, db, ct);
                    await SignIn(user, ctx);
                    return Results.Json(Profile(user), statusCode: 201);
                }
            )
            .RequireRateLimiting("register");
        app.MapPost(
                "/api/auth/login",
                async (Login input, ClinicDb db, HttpContext ctx, CancellationToken ct) =>
                {
                    // FindAsync([主键])：按主键查询，[...] 是 object[] 集合表达式（复合主键时放多个值）。
                    var user = await db.Users.FindAsync(
                        [(input.Username ?? "").Trim().ToLowerInvariant()],
                        ct
                    );
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
                    await SignIn(user, ctx);
                    return Results.Ok(Profile(user));
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
                        patientId = ctx.User.PatientScope(),
                    }
            )
            .RequireAuthorization();
    }

    public static async Task<DemoUser> Register(
        Registration input,
        ClinicDb db,
        CancellationToken ct
    )
    {
        var username = (input.Username ?? "").Trim().ToLowerInvariant();
        var name = (input.DisplayName ?? "").Trim();
        if (!Regex.IsMatch(username, "^[a-z0-9][a-z0-9_-]{2,39}$"))
            throw new BusinessException(
                "invalid_username",
                "账号须为3–40位英文字母、数字、下划线或短横线",
                400
            );
        if (name.Length is < 1 or > 80 || name.Any(char.IsControl))
            throw new BusinessException("invalid_name", "预约姓名须为1–80个可显示字符", 400);
        if (input.Password is null || input.Password.Length is < 8 or > 128)
            throw new BusinessException("invalid_password", "密码须为8–128个字符", 400);
        var user = new DemoUser
        {
            Id = username,
            Role = "Booker",
            Patient = new Patient
            {
                Name = name,
                Identifier = "USER-" + Guid.NewGuid().ToString("N"),
            },
        };
        user.PasswordHash = new PasswordHasher<DemoUser>().HashPassword(user, input.Password);
        db.Users.Add(user);
        try
        {
            // EF saves the account and its patient in one transaction. Duplicate usernames
            // roll back both rows, including when two registrations race.
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            db.ChangeTracker.Clear();
            throw new BusinessException(
                "username_taken",
                "账号已存在，请登录或换一个账号；若刚才注册响应中断，请尝试登录",
                409
            );
        }
        return user;
    }

    static object Profile(DemoUser user) =>
        new
        {
            name = user.Id,
            role = user.Role,
            patientId = user.PatientId,
        };

    static Task SignIn(DemoUser user, HttpContext ctx)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Id),
            new(ClaimTypes.Role, user.Role),
        };
        if (user.PatientId is int patientId)
            claims.Add(
                new(
                    "patient_id",
                    patientId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                )
            );
        return ctx.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(
                new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
            )
        );
    }

    public record Registration(string Username, string Password, string DisplayName);

    // record DTO：登录请求体。JSON {username, password} 自动绑定（Web 默认 camelCase 匹配）。
    public record Login(string Username, string Password);
}

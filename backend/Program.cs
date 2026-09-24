// ===== Program.cs：ASP.NET Core 应用入口与全部装配代码（≈ Spring Boot 的 Application + @Configuration 合体）=====
// C# 顶层语句（Top-level statements）：文件体即 Main 方法，无需 class Program { static void Main }。
// using = Java 的 import（注意 C# 里 using 还有"自动释放资源"的第二含义，见下文 await using）。
using System.Threading.RateLimiting;
using ClinicFlow;
using ClinicFlow.Agent;
using ClinicFlow.Fhir;
using ClinicFlow.Integration;
using ClinicFlow.Scheduling;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

// WebApplicationBuilder ≈ SpringApplication 启动前的准备阶段：收集配置、注册服务。
var builder = WebApplication.CreateBuilder(args);

// ---- DI 注册区（对照 Spring 的 @Bean 定义；ASP.NET Core 用内置容器，全部显式代码注册，无包扫描）----
// AddDbContext：注册 EF Core 的 DbContext 为 Scoped（每请求一个实例，≈ JPA 的 EntityManager 生命周期）。
builder.Services.AddDbContext<ClinicDb>(o =>
    o.UseMySql(
        // Configuration 统一读取 appsettings.json / 环境变量；?? 是 null 合并运算符（为 null 则取右侧）。
        builder.Configuration.GetConnectionString("Clinic")
            ?? throw new InvalidOperationException("ConnectionStrings__Clinic required"),
        new MySqlServerVersion(new Version(8, 4, 8))
    )
);
// Cookie 认证方案（≈ Spring Security 的 session cookie 认证）。
builder
    .Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "clinicflow.session";
        o.Cookie.HttpOnly = true; // JS 不可读，防 XSS 偷 Cookie
        o.Cookie.SameSite = SameSiteMode.Strict; // 防 CSRF 的第一道防线
        // builder.Environment ≈ Spring 的 Environment/Profiles；IsDevelopment() 判断是否开发环境。
        o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        o.ExpireTimeSpan = TimeSpan.FromHours(8); // TimeSpan ≈ Java Duration
        // API 项目不重定向到登录页，改为返回 401/403（≈ Spring 的 AuthenticationEntryPoint 定制）。
        // lambda 表达式 c => { ... } 与 Java 相同；Task.CompletedTask ≈ 立即完成的 CompletableFuture。
        o.Events.OnRedirectToLogin = c =>
        {
            c.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
        o.Events.OnRedirectToAccessDenied = c =>
        {
            c.Response.StatusCode = 403;
            return Task.CompletedTask;
        };
    });
// 命名授权策略 ≈ @PreAuthorize("hasRole('Scheduler')") 的声明式版本，端点上用 .RequireAuthorization("schedule") 引用。
builder
    .Services.AddAuthorizationBuilder()
    .AddPolicy("schedule", p => p.RequireRole("Scheduler"))
    .AddPolicy("tasks", p => p.RequireRole("TaskOperator"))
    .AddPolicy("admin", p => p.RequireRole("Admin"));
// CSRF 防护：Cookie 会话必需；约定前端在 X-CSRF-TOKEN 头回传令牌。
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");
// 内置限流中间件（≈ Bucket4j）：按客户端 IP 的固定窗口，限登录接口防爆破。
builder.Services.AddRateLimiter(o =>
    o.AddPolicy(
        "login",
        ctx =>
            RateLimitPartition.GetFixedWindowLimiter(
                // ?. 安全导航 + ?? null 合并：RemoteIpAddress 为 null 时整体取 "local"。
                ctx.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                }
            )
    )
);
// 全局 JSON 序列化设置（≈ 配置 Jackson ObjectMapper）：注册自定义 DateTime 转换器，统一输出 UTC "Z" 格式。
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new UtcDateTimeConverter())
);
// AddScoped：每请求一个实例（Spring @RequestScope）；SchedulingService 内部持有 Scoped 的 ClinicDb。
builder.Services.AddScoped<SchedulingService>();
builder.Services.AddAppointmentAgent();
// AddSingleton：全应用一个实例；接口→实现 注册（≈ Spring 的 @Bean 返回接口类型）。
builder.Services.AddSingleton<ITransactionProbe, NoTransactionProbe>();
builder.Services.AddSingleton(TimeProvider.System); // 时钟抽象，测试可替换（≈ Java Clock）
// HttpClient 工厂模式（≈ feign/RestTemplate 定制）：处理连接池与 Socket 耗尽问题。
builder.Services.AddHttpClient<Dispatcher>(client => client.Timeout = TimeSpan.FromSeconds(5));
// HostedService：随应用启停的后台任务（≈ @Scheduled 或独立线程，但由宿主管理生命周期）。
if (builder.Configuration["Integration:Enabled"] != "false")
    builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddClinicOpenApi(); // 扩展方法（见 ApiDocumentation.cs），注册 OpenAPI 文档生成
// Build()：容器定型，之后开始装中间件。
var app = builder.Build();
// 启动时建一个临时作用域取 Scoped 服务：应用迁移 + 种子数据（≈ Flyway 自动迁移 + data.sql）。
// using 块 ≈ try-with-resources：离开块自动 Dispose scope（释放 DbContext）。
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClinicDb>();
    await db.Database.MigrateAsync(); // 应用所有 pending 的 EF Migrations
    await Identity.Seed(db, app.Configuration);
}

// ---- 中间件管道区：app.Use 的顺序 = 请求流经顺序（对照 Servlet Filter 链 / HandlerInterceptor）----
// 中间件签名 async (ctx, next)：await next() 放行给下一个；不调 next() = 短路。
app.Use(
    async (ctx, next) =>
    {
        // ctx.TraceIdentifier ≈ MDC 里的 traceId；挂到响应头便于排障。
        ctx.Response.Headers["X-Correlation-ID"] = ctx.TraceIdentifier;
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        await next();
    }
);
// 全局异常映射（≈ @ControllerAdvice + @ExceptionHandler）：写在管道最外层才能包住内层所有异常。
app.Use(
    async (ctx, next) =>
    {
        try
        {
            await next();
        }
        catch (BusinessException ex) // 业务异常 → 约定状态码（默认 409）
        {
            ctx.Response.StatusCode = ex.Status;
            // new { ... } 匿名类型：编译器生成只读类，直接 JSON 序列化为错误响应。
            await ctx.Response.WriteAsJsonAsync(
                new
                {
                    code = ex.Code,
                    message = ex.Message,
                    correlationId = ctx.TraceIdentifier,
                }
            );
        }
        catch (BadHttpRequestException) // JSON 解析失败等 → 400
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsJsonAsync(
                new
                {
                    code = "invalid_request",
                    message = "请求格式不正确",
                    correlationId = ctx.TraceIdentifier,
                }
            );
        }
        // catch ... when：C# 独有的异常过滤器——只有 when 条件为真才捕获。
        // 这里把 OperationCanceledException（客户端断开/停机取消）排除在 500 之外。
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            app.Logger.LogError(ex, "Request failed {CorrelationId}", ctx.TraceIdentifier);
            ctx.Response.StatusCode = 500;
            await ctx.Response.WriteAsJsonAsync(
                new
                {
                    code = "internal_error",
                    message = "操作未完成，请使用原请求标识重试",
                    correlationId = ctx.TraceIdentifier,
                }
            );
        }
    }
);
app.UseAuthentication(); // 认证：解析 Cookie → 还原 ClaimsPrincipal（≈ SecurityContextHolder 填充）
app.UseAuthorization();  // 授权：检查端点的 .RequireAuthorization 策略
app.UseRateLimiter();
// CSRF 校验中间件：所有写操作（POST/PUT/DELETE）必须带有效 X-CSRF-TOKEN。
app.Use(
    async (ctx, next) =>
    {
        // HttpMethods.IsPost(...) 静态工具方法 ≈ "POST".equals(method)。
        if (
            HttpMethods.IsPost(ctx.Request.Method)
            || HttpMethods.IsPut(ctx.Request.Method)
            || HttpMethods.IsDelete(ctx.Request.Method)
        )
        {
            try
            {
                // ctx.RequestServices：当前请求的 Scoped 容器，可从中间件里解析 Scoped 服务。
                await ctx
                    .RequestServices.GetRequiredService<IAntiforgery>()
                    .ValidateRequestAsync(ctx);
            }
            catch (AntiforgeryValidationException)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsJsonAsync(
                    new { code = "csrf", message = "安全令牌已过期，请刷新页面" }
                );
                return; // 不调 next() → 短路，请求到此为止
            }
        }
        await next();
    }
);
// ---- 路由区：Minimal API（不写 Controller）；参数按类型/名字自动绑定 ----
// 健康检查：ClinicDb 参数由 DI 容器注入（≈ @Autowired），CanConnectAsync 探活数据库。
app.MapGet(
    "/api/health",
    async (ClinicDb db) =>
        new { status = await db.Database.CanConnectAsync() ? "ready" : "unavailable" }
);
app.MapIdentity();   // 认证端点组（扩展方法，见 Identity.cs）
app.MapAppointmentAgent();
app.MapScheduling(); // 预约端点组（见 Scheduling/Endpoints.cs）
app.MapFhir();       // FHIR R4 只读适配（见 Fhir/FhirAdapter.cs）
app.MapOpenApi("/api/openapi/{documentName}.json").RequireAuthorization();
// Outbox 队列查看（管理员）：演示 IQueryable 的链式 LINQ。
app.MapGet(
        "/api/sync",
        // CancellationToken ct：客户端断开时取消整条异步链（无 Java 直接对应）。
        async (ClinicDb db, CancellationToken ct) =>
            await db
                .Outbox.AsNoTracking() // AsNoTracking：只读查询不做变更跟踪（≈ JPA read-only hint）
                .OrderByDescending(x => x.NextAttemptUtc)
                .ThenBy(x => x.Id)
                .Take(100)
                .Select(x => new // Select 投影到匿名类型 → 只查询这些列，翻译成 SQL SELECT 指定列
                {
                    x.Id,
                    x.AppointmentId,
                    x.Version,
                    x.Status,
                    x.Attempts,
                    x.LastError,
                    x.NextAttemptUtc,
                })
                .ToListAsync(ct) // ToListAsync 是执行边界：这里才真正发 SQL
    )
    .RequireAuthorization("admin");
// {id} 路由占位参数自动绑定到 string id（≈ @PathVariable）。
app.MapGet(
        "/api/sync/{id}/attempts",
        async (string id, ClinicDb db, CancellationToken ct) =>
            await db.Set<SyncAttempt>() // Set<T>()：无显式 DbSet 属性时也可按类型取表
                .AsNoTracking()
                .Where(x => x.MessageId == id)
                .OrderBy(x => x.Id)
                .ToListAsync(ct)
    )
    .RequireAuthorization("admin");
app.MapPost(
        "/api/sync/{id}/retry",
        async (string id, SchedulingService service, HttpContext ctx, CancellationToken ct) =>
            await service.RetrySync(
                id,
                // ! 是空断言运算符："我保证非空，编译器别警告"（不生成运行时代码）。
                ctx.User.Identity!.Name!,
                ctx.Request.Headers["Idempotency-Key"].ToString(),
                ctx.TraceIdentifier,
                ct
            )
    )
    .RequireAuthorization("admin");
app.MapGet(
        "/api/patients",
        async (ClinicDb db, CancellationToken ct) =>
            await db.Patients.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct)
    )
    .RequireAuthorization();
app.MapGet(
        "/api/resources",
        async (ClinicDb db, CancellationToken ct) =>
            await db.Resources.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct)
    )
    .RequireAuthorization();
// SPA 静态文件托管：UseDefaultFiles/UseStaticFiles ≈ Spring 的 classpath:/static 资源映射。
app.UseDefaultFiles();
app.UseStaticFiles();
// API 兜底：{**path} 通配路由，未匹配的 /api/* 返回统一 404。
app.MapMethods(
        "/api/{**path}",
        ["GET", "POST", "PUT", "DELETE", "PATCH"], // C# 12 集合表达式 ≈ List.of(...)
        () => Results.NotFound(new { code = "not_found" })
    )
    .ExcludeFromDescription(); // 从 OpenAPI 文档中排除
// 前端路由兜底：非 /api 路径都回退到 index.html（SPA history 模式）。
app.MapFallbackToFile("index.html");
app.Run(); // 启动 Kestrel（内置 Web 服务器 ≈ 内嵌 Tomcat）并阻塞监听

// partial class：让测试项目的 WebApplicationFactory<Program> 能找到入口类型（顶层语句会生成隐藏的 Program）。
public partial class Program;

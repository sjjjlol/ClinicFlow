using System;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using ClinicFlow.Middleware;
using ClinicFlow.Ois;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClinicFlow
{
    public class Startup
    {
        private readonly IWebHostEnvironment environment;

        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            Configuration = configuration;
            this.environment = environment;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // ---- DI 注册区（对照 Spring 的 @Bean 定义；ASP.NET Core 用内置容器，全部显式代码注册，无包扫描）----
            // AddDbContext：注册 EF Core 的 DbContext 为 Scoped（每请求一个实例，≈ JPA 的 EntityManager 生命周期）。
            services.AddDbContext<ClinicDb>(o =>
                o.UseMySql(
                    // Configuration 统一读取 appsettings.json / 环境变量；?? 是 null 合并运算符（为 null 则取右侧）。
                    Configuration.GetConnectionString("Clinic")
                        ?? throw new InvalidOperationException("ConnectionStrings__Clinic required"),
                    new MySqlServerVersion(new Version(8, 4, 8))
                )
            );

            // Cookie 认证方案（≈ Spring Security 的 session cookie 认证）。
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(o =>
                {
                    o.Cookie.Name = "clinicflow.session";
                    o.Cookie.HttpOnly = true; // JS 不可读，防 XSS 偷 Cookie
                    o.Cookie.SameSite = SameSiteMode.Strict; // 防 CSRF 的第一道防线
                    // environment ≈ Spring 的 Environment/Profiles；IsDevelopment() 判断是否开发环境。
                    o.Cookie.SecurePolicy = environment.IsDevelopment()
                        ? CookieSecurePolicy.SameAsRequest
                        : CookieSecurePolicy.Always;
                    o.ExpireTimeSpan = TimeSpan.FromHours(8); // TimeSpan ≈ Java Duration
                    // API 项目不重定向到登录页，改为返回 401/403（≈ Spring 的 AuthenticationEntryPoint 定制）。
                    // lambda 表达式 c => { ... } 与 Java 相同；Task.CompletedTask ≈ 立即完成的 CompletableFuture。
                    o.Events.OnValidatePrincipal = async c =>
                    {
                        if (c.Principal == null || !Identity.IsStaff(c.Principal))
                        {
                            c.RejectPrincipal();
                            await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignOutAsync(c.HttpContext);
                        }
                    };
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

            // 命名授权策略 ≈ @PreAuthorize("hasRole('Scheduler')") 的声明式版本，端点上用 [Authorize(Policy = "ois")] 引用。
            services.AddAuthorizationBuilder()
                .AddPolicy("ois", p => p.RequireRole("Scheduler", "Clinician", "Physicist"));

            // CSRF 防护：Cookie 会话必需；约定前端在 X-CSRF-TOKEN 头回传令牌。
            services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");

            // 内置限流中间件（≈ Bucket4j）：按客户端 IP 的固定窗口，限登录接口防爆破。
            services.AddRateLimiter(o =>
            {
                o.RejectionStatusCode = 429;
                o.OnRejected = async (context, ct) =>
                {
                    var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                        ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                        : 60;
                    context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(
                        System.Globalization.CultureInfo.InvariantCulture
                    );
                    await context.HttpContext.Response.WriteAsJsonAsync(
                        new
                        {
                            code = "rate_limited",
                            message = $"操作过于频繁，请在{seconds}秒后重试",
                            correlationId = context.HttpContext.TraceIdentifier,
                        },
                        cancellationToken: ct
                    );
                };
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
                );
            });

            // MVC 使用独立的 JSON 配置，保持原接口的 UTC 日期格式和错误结构。
            services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
                .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter()))
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(new
                    {
                        code = "invalid_request",
                        message = "请求格式不正确",
                        correlationId = context.HttpContext.TraceIdentifier,
                    });
                });
            services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new UtcDateTimeConverter()));

            services.AddScoped<OisService>();
            services.AddSingleton(TimeProvider.System);
            services.AddClinicOpenApi();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseMiddleware<ResponseHeadersMiddleware>();
            app.UseMiddleware<ApiExceptionMiddleware>();
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();
            app.UseMiddleware<CsrfMiddleware>();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapOpenApi("/api/openapi/{documentName}.json").RequireAuthorization("ois");
                endpoints.MapFallbackToFile("/", "index.html");
            });
        }
    }
}

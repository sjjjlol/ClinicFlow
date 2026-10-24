using System;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using ClinicFlow.Ois;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClinicFlow.Hosting
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddClinicServices(
            this IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            services.AddClinicInfrastructure(configuration);
            services.AddClinicAuthentication(environment);
            services.AddClinicHttp();
            services.AddClinicOis();
            return services;
        }

        public static IServiceCollection AddClinicOis(this IServiceCollection services)
        {
            services.AddScoped<CommandExecutor>();
            services.AddSingleton(TimeProvider.System);
            return services;
        }

        private static IServiceCollection AddClinicAuthentication(this IServiceCollection services, IHostEnvironment environment)
        {
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

            return services;
        }

        private static IServiceCollection AddClinicHttp(this IServiceCollection services)
        {
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

            services.AddClinicOpenApi();
            return services;
        }
    }
}

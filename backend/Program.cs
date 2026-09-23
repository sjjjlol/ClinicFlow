using ClinicFlow.Scheduling;
using ClinicFlow;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ClinicDb>(o => o.UseMySql(builder.Configuration.GetConnectionString("Clinic") ?? throw new InvalidOperationException("ConnectionStrings__Clinic required"), new MySqlServerVersion(new Version(8,4,8))));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "clinicflow.session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("schedule", p => p.RequireRole("Scheduler"))
    .AddPolicy("tasks", p => p.RequireRole("TaskOperator"))
    .AddPolicy("admin", p => p.RequireRole("Admin"));
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddRateLimiter(o => o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) })));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new UtcDateTimeConverter()));
builder.Services.AddScoped<SchedulingService>();
builder.Services.AddSingleton<ITransactionProbe, NoTransactionProbe>();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClinicDb>();
    await db.Database.MigrateAsync();
    await Identity.Seed(db, app.Configuration);
}
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Correlation-ID"] = ctx.TraceIdentifier;
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await next();
});
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (BusinessException ex) { ctx.Response.StatusCode = ex.Status; await ctx.Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message, correlationId = ctx.TraceIdentifier }); }
    catch (Exception ex) when (ex is not OperationCanceledException) { app.Logger.LogError(ex, "Request failed {CorrelationId}", ctx.TraceIdentifier); ctx.Response.StatusCode = 500; await ctx.Response.WriteAsJsonAsync(new { code = "internal_error", message = "操作未完成，请使用原请求标识重试", correlationId = ctx.TraceIdentifier }); }
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (ctx, next) =>
{
    if (HttpMethods.IsPost(ctx.Request.Method) || HttpMethods.IsPut(ctx.Request.Method) || HttpMethods.IsDelete(ctx.Request.Method))
    {
        try { await ctx.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(ctx); }
        catch (AntiforgeryValidationException) { ctx.Response.StatusCode = 400; await ctx.Response.WriteAsJsonAsync(new { code = "csrf", message = "安全令牌已过期，请刷新页面" }); return; }
    }
    await next();
});
app.MapGet("/api/health", async (ClinicDb db) => new { status = await db.Database.CanConnectAsync() ? "ready" : "unavailable" });
app.MapIdentity();
app.MapScheduling();
app.MapGet("/api/patients", async (ClinicDb db, CancellationToken ct) => await db.Patients.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct)).RequireAuthorization();
app.MapGet("/api/resources", async (ClinicDb db, CancellationToken ct) => await db.Resources.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct)).RequireAuthorization();
app.Run();
public partial class Program;

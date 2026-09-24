using System.Threading.RateLimiting;

namespace ClinicFlow.Agent;

public static class AgentEndpoints
{
    public static void AddAppointmentAgent(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
            options.AddPolicy(
                "agent-messages",
                ctx =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        ctx.User.Identity?.Name ?? "anonymous",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 12,
                            Window = TimeSpan.FromMinutes(1),
                        }
                    )
            )
        );
        services.AddSingleton<AgentSessions>();
        services.AddScoped<Availability>();
        services.AddScoped<AppointmentAgent>();
        services.AddHttpClient<IAgentModel, KimiAgentModel>(c =>
            c.Timeout = TimeSpan.FromSeconds(45)
        );
    }

    public static void MapAppointmentAgent(this WebApplication app)
    {
        var group = app.MapGroup("/api/agent").RequireAuthorization("schedule");
        var demo =
            app.Environment.IsDevelopment() && app.Configuration["Agent:DemoEnabled"] == "true";
        group.MapGet(
            "/config",
            (IConfiguration config) =>
                new
                {
                    configured = !string.IsNullOrWhiteSpace(config["KIMI_API_KEY"]),
                    demoEnabled = demo,
                    timeZone = "Asia/Shanghai",
                    model = config["Agent:Model"] ?? "kimi-k2.6",
                }
        );
        group
            .MapPost(
                "/messages",
                (
                    AgentMessage input,
                    AppointmentAgent agent,
                    HttpContext ctx,
                    CancellationToken ct
                ) => agent.Message(input, ctx.User.Identity!.Name!, ct)
            )
            .RequireRateLimiting("agent-messages");
        group.MapPost(
            "/{sessionId}/confirm",
            (
                string sessionId,
                ConfirmCandidate input,
                AppointmentAgent agent,
                HttpContext ctx,
                CancellationToken ct
            ) =>
                agent.Confirm(
                    sessionId,
                    input.CandidateId,
                    ctx.User.Identity!.Name!,
                    ctx.TraceIdentifier,
                    ct
                )
        );
        if (demo)
            group.MapPost(
                "/{sessionId}/simulate-conflict",
                (
                    string sessionId,
                    ConfirmCandidate input,
                    AppointmentAgent agent,
                    HttpContext ctx,
                    CancellationToken ct
                ) =>
                    agent.SimulateConflict(
                        sessionId,
                        input.CandidateId,
                        ctx.User.Identity!.Name!,
                        ctx.TraceIdentifier,
                        ct
                    )
            );
    }
}

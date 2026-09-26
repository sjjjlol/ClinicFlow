using System.Text.Json;
using ClinicFlow.Scheduling;
using Microsoft.AspNetCore.Http.Features;

namespace ClinicFlow.Agent;

public record AgentEvent(
    string Type,
    string? Text = null,
    string? Message = null,
    string? SessionId = null,
    ToolTrace? Trace = null,
    AgentReply? Reply = null,
    string? Code = null,
    int? Status = null
);

public static class AgentStream
{
    // Authentication, authorization and CSRF run before the response starts. Once
    // streaming, failures are framed events rather than a second HTTP response.
    public static async Task Write(
        HttpContext ctx,
        Func<Func<AgentEvent, Task>, Task<AgentReply>> run
    )
    {
        ctx.Response.ContentType = "text/event-stream; charset=utf-8";
        ctx.Response.Headers.CacheControl = "no-cache, no-store";
        ctx.Response.Headers["X-Accel-Buffering"] = "no";
        ctx.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        async Task Emit(AgentEvent value)
        {
            await ctx.Response.WriteAsync(
                "data: " + JsonSerializer.Serialize(value, SchedulingService.Json) + "\n\n",
                ctx.RequestAborted
            );
            await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
        }
        try
        {
            await Emit(new("progress", Message: "已连接预约助手…"));
            var reply = await run(Emit);
            await Emit(new("result", Reply: reply));
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested) { }
        catch (IOException)
        { /* Client transport closed; any committed confirmation stays replayable. */
        }
        catch (Exception ex)
        {
            if (ctx.RequestAborted.IsCancellationRequested)
                return;
            var business = ex as BusinessException;
            await Emit(
                new(
                    "error",
                    Message: business?.Message
                        ?? "连接中断，请重试；确认结果不明时请重试同一候选或查看预约列表",
                    Code: business?.Code ?? "stream_failed",
                    Status: business?.Status ?? 503
                )
            );
        }
    }
}

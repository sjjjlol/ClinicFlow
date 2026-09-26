using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using ClinicFlow.Scheduling;

namespace ClinicFlow.Agent;

public static class PiAgentRuntime
{
    static readonly SemaphoreSlim Capacity = new(4);

    public static async Task<JsonArray> Run(
        JsonArray history,
        string instructions,
        bool recovering,
        Func<JsonArray, Func<string, Task>, Task<JsonObject>> respond,
        Func<string, string, Task<JsonNode>> execute,
        IConfiguration? config,
        CancellationToken ct
    )
    {
        if (!await Capacity.WaitAsync(TimeSpan.FromSeconds(5), ct))
            throw new BusinessException("agent_busy", "预约助手繁忙，请稍后重试", 429);
        try
        {
            return await RunCore(history, instructions, recovering, respond, execute, config, ct);
        }
        finally
        {
            Capacity.Release();
        }
    }

    static async Task<JsonArray> RunCore(
        JsonArray history,
        string instructions,
        bool recovering,
        Func<JsonArray, Func<string, Task>, Task<JsonObject>> respond,
        Func<string, string, Task<JsonNode>> execute,
        IConfiguration? config,
        CancellationToken ct
    )
    {
        var script = config?["Agent:RuntimePath"] ?? Locate();
        var start = new ProcessStartInfo(config?["Agent:NodePath"] ?? "node")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
        };
        start.ArgumentList.Add(script);
        // The runtime needs no database password or model key. Model HTTP stays in .NET.
        start.Environment.Clear();
        start.Environment["PATH"] = Environment.GetEnvironmentVariable("PATH");
        using var process = Process.Start(start) ?? throw Unavailable();
        var stderr = process.StandardError.ReadToEndAsync(ct);
        using var cancellation = ct.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(true);
            }
            catch (InvalidOperationException) { }
        });
        async Task Write(object value) =>
            await process.StandardInput.WriteLineAsync(
                System.Text.Json.JsonSerializer.Serialize(value, SchedulingService.Json).AsMemory(),
                ct
            );
        try
        {
            await Write(
                new
                {
                    type = "init",
                    history,
                    instructions,
                    recovering,
                    tools = KimiAgentModel.Tools,
                }
            );
            while (await process.StandardOutput.ReadLineAsync(ct) is { } line)
            {
                if (line.Length > 2_000_000)
                    throw Unavailable();
                var frame = JsonNode.Parse(line) ?? throw Unavailable();
                var id = frame["id"]?.GetValue<int>();
                switch (frame["type"]?.GetValue<string>())
                {
                    case "model":
                        var answer = await respond(
                            frame["history"]!.AsArray(),
                            text =>
                                Write(
                                    new
                                    {
                                        type = "delta",
                                        id,
                                        text,
                                    }
                                )
                        );
                        await Write(
                            new
                            {
                                type = "response",
                                id,
                                value = answer,
                            }
                        );
                        break;
                    case "tool":
                        var value = await execute(
                            frame["name"]!.GetValue<string>(),
                            frame["args"]!.ToJsonString()
                        );
                        await Write(
                            new
                            {
                                type = "response",
                                id,
                                value,
                            }
                        );
                        break;
                    case "done":
                        return frame["history"]!.AsArray();
                    default:
                        throw Unavailable();
                }
            }
            ct.ThrowIfCancellationRequested();
            throw Unavailable();
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(true);
            await process.WaitForExitAsync(CancellationToken.None);
            try
            {
                await stderr;
            }
            catch (OperationCanceledException) { }
        }
    }

    static string Locate()
    {
        for (
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            dir is not null;
            dir = dir.Parent
        )
        {
            var path = Path.Combine(dir.FullName, "agent-runtime", "worker.mjs");
            if (File.Exists(path))
                return path;
        }
        throw Unavailable();
    }

    static BusinessException Unavailable() =>
        new("agent_runtime_unavailable", "预约助手运行时暂不可用，请重试或使用表单", 503);
}

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClinicFlow.Scheduling;

namespace ClinicFlow.Agent;

public interface IAgentModel
{
    Task<JsonObject> Respond(JsonArray input, string instructions, CancellationToken ct);

    async Task<JsonObject> RespondStreaming(
        JsonArray input,
        string instructions,
        Func<string, Task> onText,
        CancellationToken ct
    )
    {
        var reply = await Respond(input, instructions, ct);
        if (reply["content"]?.GetValue<string>() is { Length: > 0 } text)
            await onText(text);
        return reply;
    }
}

// A small Kimi Chat Completions API adapter keeps the agent loop independent of any vendor SDK.
public class KimiAgentModel(HttpClient client, IConfiguration configuration) : IAgentModel
{
    public Task<JsonObject> Respond(JsonArray input, string instructions, CancellationToken ct) =>
        RespondCore(input, instructions, null, ct);

    public Task<JsonObject> RespondStreaming(
        JsonArray input,
        string instructions,
        Func<string, Task> onText,
        CancellationToken ct
    ) => RespondCore(input, instructions, onText, ct);

    async Task<JsonObject> RespondCore(
        JsonArray input,
        string instructions,
        Func<string, Task>? onText,
        CancellationToken ct
    )
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        ct = deadline.Token;
        var key = configuration["KIMI_API_KEY"];
        if (string.IsNullOrWhiteSpace(key))
            throw new BusinessException(
                "agent_unavailable",
                "预约助手尚未配置模型密钥，请使用新建预约表单",
                503
            );
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint(configuration));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = JsonContent.Create(
            new
            {
                model = configuration["Agent:Model"] ?? "kimi-k2.6",
                messages = new JsonArray(
                    new[]
                    {
                        JsonSerializer.SerializeToNode(
                            new { role = "system", content = instructions }
                        ),
                    }
                        .Concat(input.Select(x => x!.DeepClone()))
                        .ToArray()
                ),
                tools = Tools,
                thinking = new { type = "disabled" },
                temperature = 0.6,
                max_tokens = 1600,
                stream = onText is not null,
            }
        );
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct
        );
        if (!response.IsSuccessStatusCode)
            throw new BusinessException(
                "model_unavailable",
                response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized => "Kimi密钥无效，请检查后端配置",
                    System.Net.HttpStatusCode.PaymentRequired =>
                        "Kimi账户额度不足，请检查开放平台账户",
                    System.Net.HttpStatusCode.TooManyRequests => "Kimi请求限流，请稍后重试",
                    _ =>
                        $"Kimi服务返回HTTP {(int)response.StatusCode}，请重试或使用表单；未创建预约",
                },
                503
            );
        if (onText is not null)
            return await ReadStream(response, onText, ct);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>(ct);
        var choice = body?["choices"]?[0];
        if (
            choice?["finish_reason"]?.GetValue<string>() is not ("stop" or "tool_calls")
            || choice["message"] is not JsonObject output
        )
            throw new BusinessException(
                "model_incomplete",
                "模型未完成本轮，请重试或使用表单；未创建预约",
                503
            );
        return output;
    }

    static async Task<JsonObject> ReadStream(
        HttpResponseMessage response,
        Func<string, Task> onText,
        CancellationToken ct
    )
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        var text = new System.Text.StringBuilder();
        var calls = new SortedDictionary<int, JsonObject>();
        string? finish = null;
        var done = false;
        var size = 0;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data:"))
                continue;
            var data = line[5..].Trim();
            if (data == "[DONE]")
            {
                done = true;
                break;
            }
            if (data.Length == 0)
                continue;
            size += data.Length;
            if (size > 1_000_000)
                throw Incomplete();
            var chunk = JsonNode.Parse(data);
            if (chunk?["error"] is not null)
                throw Incomplete();
            var choice = chunk?["choices"]?.AsArray().FirstOrDefault();
            if (choice is null)
                continue; // optional usage-only frame
            if (choice["finish_reason"]?.GetValue<string>() is { } reason)
                finish = reason;
            if (choice["delta"] is not JsonObject delta)
                continue;
            // Reasoning and partial tool arguments never leave this adapter.
            if (delta["content"]?.GetValue<string>() is { Length: > 0 } part)
            {
                text.Append(part);
                await onText(part);
            }
            if (delta["tool_calls"] is not JsonArray fragments)
                continue;
            foreach (var fragment in fragments)
            {
                var index = fragment?["index"]?.GetValue<int>() ?? throw Incomplete();
                if (index is < 0 or > 7)
                    throw Incomplete();
                if (!calls.TryGetValue(index, out var call))
                {
                    call = new JsonObject
                    {
                        ["id"] = "",
                        ["type"] = "function",
                        ["function"] = new JsonObject { ["name"] = "", ["arguments"] = "" },
                    };
                    calls.Add(index, call);
                }
                if (fragment!["id"]?.GetValue<string>() is { } id)
                    call["id"] = call["id"]!.GetValue<string>() + id;
                var function = call["function"]!.AsObject();
                foreach (var field in new[] { "name", "arguments" })
                    if (fragment["function"]?[field]?.GetValue<string>() is { } value)
                        function[field] = function[field]!.GetValue<string>() + value;
            }
        }
        if (!done || finish is not ("stop" or "tool_calls"))
            throw Incomplete();
        if ((finish == "tool_calls") != (calls.Count > 0))
            throw Incomplete();
        if (
            calls.Values.Any(c =>
                string.IsNullOrEmpty(c["id"]!.GetValue<string>())
                || string.IsNullOrEmpty(c["function"]!["name"]!.GetValue<string>())
            )
        )
            throw Incomplete();
        var answer = new JsonObject { ["role"] = "assistant", ["content"] = text.ToString() };
        if (calls.Count > 0)
            answer["tool_calls"] = new JsonArray(calls.Values.Select(c => (JsonNode)c).ToArray());
        return answer;
    }

    static BusinessException Incomplete() =>
        new("model_incomplete", "模型回复中断，请重试或使用表单；未创建预约", 503);

    static string Endpoint(IConfiguration configuration)
    {
        var url = configuration["Agent:BaseUrl"] ?? "https://api.moonshot.cn/v1";
        if (url is not ("https://api.moonshot.cn/v1" or "https://api.moonshot.ai/v1"))
            throw new BusinessException(
                "agent_configuration",
                "请配置 Moonshot 官方 API 地址",
                503
            );
        return url + "/chat/completions";
    }

    public static readonly JsonArray Tools = JsonNode
        .Parse(
            """
            [
              {"type":"function","function":{"name":"list_catalog","description":"查询当前账号允许访问的预约档案与资源。不能假设编号。",
               "parameters":{"type":"object","properties":{},"required":[],"additionalProperties":false}}},
              {"type":"function","function":{"name":"search_slots","description":"按用户明确的约束查询并展示最多3个候选，不会创建预约。必须先取得患者、时长、日期范围；不可擅自扩大范围。每日时间用午夜后的分钟数，下午=720到1020。",
               "parameters":{"type":"object","properties":{
                 "patientId":{"type":["integer","null"]},"fromDate":{"type":["string","null"]},"toDate":{"type":["string","null"]},
                 "durationMinutes":{"type":["integer","null"]},"resourceId":{"type":["integer","null"]},
                 "earliestMinute":{"type":["integer","null"]},"latestMinute":{"type":["integer","null"]}},
                 "required":["patientId","fromDate","toDate","durationMinutes","resourceId","earliestMinute","latestMinute"],"additionalProperties":false}}}
            ]
            """
        )!
        .AsArray();
}

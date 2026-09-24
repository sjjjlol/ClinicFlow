using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClinicFlow.Scheduling;

namespace ClinicFlow.Agent;

public interface IAgentModel
{
    Task<JsonObject> Respond(JsonArray input, string instructions, CancellationToken ct);
}

// A small Kimi Chat Completions API adapter keeps the agent loop independent of any vendor SDK.
public class KimiAgentModel(HttpClient client, IConfiguration configuration) : IAgentModel
{
    public async Task<JsonObject> Respond(
        JsonArray input,
        string instructions,
        CancellationToken ct
    )
    {
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
            }
        );
        using var response = await client.SendAsync(request, ct);
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
              {"type":"function","function":{"name":"list_catalog","description":"查询允许的虚构患者与预约资源。不能假设编号。",
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

using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ClinicFlow.Agent;
using ClinicFlow.Scheduling;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ClinicFlow.Tests;

public class KimiModelTests
{
    [Fact]
    public async Task UsesKimiProtocolAndNeverSendsAWriteTool()
    {
        var handler = new Stub(async request =>
        {
            Assert.Equal(
                "https://api.moonshot.cn/v1/chat/completions",
                request.RequestUri!.ToString()
            );
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync())!;
            Assert.Equal("disabled", body["thinking"]!["type"]!.GetValue<string>());
            Assert.Equal("system", body["messages"]![0]!["role"]!.GetValue<string>());
            Assert.Equal("user", body["messages"]![1]!["role"]!.GetValue<string>());
            Assert.Equal(
                new[] { "list_catalog", "search_slots" },
                body["tools"]!
                    .AsArray()
                    .Select(t => t!["function"]!["name"]!.GetValue<string>())
                    .ToArray()
            );
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"choices":[{"finish_reason":"stop","message":{"role":"assistant","content":"请补充日期"}}]}"""
                ),
            };
        });
        var model = new KimiAgentModel(new HttpClient(handler), Config("test-key"));
        var result = await model.Respond(
            [new JsonObject { ["role"] = "user", ["content"] = "预约" }],
            "rules",
            default
        );
        Assert.Equal("请补充日期", result["content"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(401)]
    [InlineData(402)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task ProviderErrorsDoNotExposeRawBodies(int status)
    {
        var model = new KimiAgentModel(
            new HttpClient(
                new Stub(_ =>
                    Task.FromResult(
                        new HttpResponseMessage((HttpStatusCode)status)
                        {
                            Content = new StringContent("secret-provider-body"),
                        }
                    )
                )
            ),
            Config("test-key")
        );
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            model.Respond([], "rules", default)
        );
        Assert.DoesNotContain("secret-provider-body", ex.Message);
        Assert.Equal(503, ex.Status);
    }

    [Fact]
    public async Task MissingKeyDoesNotMakeNetworkRequest()
    {
        var model = new KimiAgentModel(
            new HttpClient(new Stub(_ => throw new Exception("network must not run"))),
            Config(null)
        );
        Assert.Equal(
            "agent_unavailable",
            (
                await Assert.ThrowsAsync<BusinessException>(() =>
                    model.Respond([], "rules", default)
                )
            ).Code
        );
    }

    [Fact]
    public async Task RealStreamYieldsTextBeforeDoneAndNeverExposesReasoning()
    {
        var pipe = new Pipe();
        var model = new KimiAgentModel(
            new HttpClient(
                new Stub(async request =>
                {
                    var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync())!;
                    Assert.True(body["stream"]!.GetValue<bool>());
                    return new(HttpStatusCode.OK)
                    {
                        Content = new StreamContent(pipe.Reader.AsStream()),
                    };
                })
            ),
            Config("test-key")
        );
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var text = "";
        var result = model.RespondStreaming(
            [],
            "rules",
            part =>
            {
                text += part;
                arrived.TrySetResult();
                return Task.CompletedTask;
            },
            default
        );
        var bytes = Encoding.UTF8.GetBytes(
            "data: {\"choices\":[{\"delta\":{\"content\":\"你好\",\"reasoning_content\":\"private-reasoning\"},\"finish_reason\":null}]}\n\n"
        );
        // Split even in the middle of a UTF-8 code point.
        for (var i = 0; i < bytes.Length; i++)
            await pipe.Writer.WriteAsync(bytes.AsMemory(i, 1));
        await arrived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result.IsCompleted);
        Assert.Equal("你好", text);
        await pipe.Writer.WriteAsync(
            Encoding.UTF8.GetBytes(
                "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n"
            )
        );
        await pipe.Writer.CompleteAsync();
        Assert.Equal("你好", (await result)["content"]!.GetValue<string>());
        Assert.DoesNotContain("private-reasoning", text);
    }

    [Fact]
    public async Task StreamingReassemblesFragmentedToolArguments()
    {
        var frames = new[]
        {
            """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"a","function":{"name":"search_slots","arguments":"{\"patientId\":"}}]},"finish_reason":null}]}""",
            """{"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"3}"}}]},"finish_reason":"tool_calls"}]}""",
            """{"choices":[],"usage":{}}""",
            "[DONE]",
        };
        var model = new KimiAgentModel(
            new HttpClient(
                new Stub(_ =>
                    Task.FromResult(
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                                string.Join("", frames.Select(f => "data: " + f + "\n\n"))
                            ),
                        }
                    )
                )
            ),
            Config("test-key")
        );
        var result = await model.RespondStreaming(
            [],
            "rules",
            _ => throw new Exception("Tool arguments are not user text"),
            default
        );
        Assert.Equal(
            "search_slots",
            result["tool_calls"]![0]!["function"]!["name"]!.GetValue<string>()
        );
        Assert.Equal(
            3,
            JsonNode.Parse(
                result["tool_calls"]![0]!["function"]!["arguments"]!.GetValue<string>()
            )!["patientId"]!.GetValue<int>()
        );
    }

    [Theory]
    [InlineData(
        "data: {\"choices\":[{\"delta\":{\"content\":\"partial\"},\"finish_reason\":null}]}\n\n"
    )]
    [InlineData(
        "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"length\"}]}\n\ndata: [DONE]\n\n"
    )]
    public async Task TruncatedStreamsAreRejected(string body)
    {
        var model = new KimiAgentModel(
            new HttpClient(
                new Stub(_ =>
                    Task.FromResult(
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(body),
                        }
                    )
                )
            ),
            Config("test-key")
        );
        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            model.RespondStreaming([], "rules", _ => Task.CompletedTask, default)
        );
        Assert.Equal("model_incomplete", error.Code);
    }

    static IConfiguration Config(string? key) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["KIMI_API_KEY"] = key })
            .Build();

    class Stub(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => respond(request);
    }
}

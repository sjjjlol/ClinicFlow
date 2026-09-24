using System.Net;
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

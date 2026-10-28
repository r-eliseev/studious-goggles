using System.Net;
using System.Text.Json.Nodes;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.OpenAiCompatible;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Tests.TestDoubles;

namespace ImageGen.Core.Tests.OpenAiCompatible;

public class OpenAiCompatibleClientTests
{
    private const string Key = "sk-local-1234";
    private const string Json = """
        {"providers":[{"id":"local","kind":"openAiCompatible","displayName":"Local","baseAddress":"http://localhost:1234/v1",
          "timeoutSeconds":5,
          "imageModels":[{"id":"sd-xl","name":"SD XL","supportsSeed":true,"maxImagesPerRequest":1}]}]}
        """;
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    private static OpenAiCompatibleClient CreateClient(HttpMessageHandler handler, string? storedKey = Key)
    {
        var secrets = Substitute.For<ISecretStore>();
        secrets.GetAsync("provider.local.api_key", Arg.Any<CancellationToken>()).Returns(storedKey);
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("local").Returns(new HttpClient(handler));
        var config = ProviderConfigLoader.Build(Json, null).Get("local");
        return new OpenAiCompatibleClient(config, factory, secrets, time);
    }

    [Fact]
    public async Task ListImageModelsAsync_ConfiguredModels_ReturnsThemWithoutRequest()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
        var client = CreateClient(handler);

        var models = await client.ListImageModelsAsync(TestContext.Current.CancellationToken);

        var model = Assert.Single(models);
        Assert.Equal("local", model.ProviderId);
        Assert.Equal("sd-xl", model.Id);
        Assert.Equal("SD XL", model.Name);
        Assert.True(model.SupportsSeed);
        Assert.Equal(Now, model.FetchedAt);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GenerateAsync_WithSeed_PostsToImagesGenerationsWithBearerKey()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """{"data":[{"b64_json":"AQID"}]}""");
        var client = CreateClient(handler);

        var result = await client.GenerateAsync(new ImageGenerationRequest("sd-xl", "a fox", 42), TestContext.Current.CancellationToken);

        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://localhost:1234/v1/images/generations", request.RequestUri!.ToString());
        Assert.Equal($"Bearer {Key}", request.Headers.Authorization!.ToString());
        var body = JsonNode.Parse(handler.RequestBodies[0])!;
        Assert.Equal("sd-xl", body["model"]!.GetValue<string>());
        Assert.Equal("a fox", body["prompt"]!.GetValue<string>());
        Assert.Equal(42, body["seed"]!.GetValue<long>());
        Assert.Equal([1, 2, 3], result.Content);
        Assert.Equal("image/png", result.MediaType);
        Assert.Null(result.CostUsd);
    }

    [Fact]
    public async Task GenerateAsync_WithoutSeed_OmitsSeed()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """{"data":[{"b64_json":"AQID"}]}""");
        var client = CreateClient(handler);

        await client.GenerateAsync(new ImageGenerationRequest("sd-xl", "p", null), TestContext.Current.CancellationToken);

        Assert.False(JsonNode.Parse(handler.RequestBodies[0])!.AsObject().ContainsKey("seed"));
    }

    [Fact]
    public async Task GenerateAsync_UrlOnlyReply_ThrowsEmptyResponse()
    {
        var client = CreateClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, """{"data":[{"url":"https://x.test/a.png"}]}"""));

        var ex = await Assert.ThrowsAsync<ProviderException>(() =>
            client.GenerateAsync(new ImageGenerationRequest("sd-xl", "p", null), TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.EmptyResponse, ex.Kind);
    }

    [Fact]
    public async Task GenerateAsync_NoKeySaved_ThrowsInvalidKeyWithoutRequest()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
        var client = CreateClient(handler, storedKey: null);

        var ex = await Assert.ThrowsAsync<ProviderException>(() =>
            client.GenerateAsync(new ImageGenerationRequest("sd-xl", "p", null), TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.InvalidKey, ex.Kind);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GenerateAsync_RateLimitError_MapsKindAndKeepsProviderMessage()
    {
        const string json = """{"error":{"code":429,"message":"Too many requests for sd-xl"}}""";
        var client = CreateClient(StubHttpMessageHandler.Json((HttpStatusCode)429, json));

        var ex = await Assert.ThrowsAsync<ProviderException>(() =>
            client.GenerateAsync(new ImageGenerationRequest("sd-xl", "p", null), TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.RateLimited, ex.Kind);
        Assert.Equal("Too many requests for sd-xl", ex.Message);
        Assert.DoesNotContain(Key, ex.Message);
    }

    [Fact]
    public async Task GenerateAsync_NetworkFailure_ThrowsNetwork()
    {
        var client = CreateClient(StubHttpMessageHandler.Throws(new HttpRequestException("down")));

        var ex = await Assert.ThrowsAsync<ProviderException>(() =>
            client.GenerateAsync(new ImageGenerationRequest("sd-xl", "p", null), TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.Network, ex.Kind);
    }

    [Fact]
    public async Task GenerateAsync_NoAnswerInTime_ThrowsTimeout()
    {
        var client = CreateClient(new HangingHandler());

        var ex = await Assert.ThrowsAsync<ProviderException>(() =>
            client.GenerateAsync(new ImageGenerationRequest("sd-xl", "p", null), TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.Timeout, ex.Kind);
    }

    [Fact]
    public async Task CompleteChatAsync_WithTool_PostsToChatCompletionsAndReadsToolCall()
    {
        const string json = """
            {"choices":[{"message":{"content":null,"tool_calls":[
              {"type":"function","function":{"name":"generate_images","arguments":"{\"prompt\":\"fox\"}"}}]}}]}
            """;
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, json);
        var client = CreateClient(handler);
        var request = new AssistantChatRequest(
            "gpt-local",
            [AssistantChatMessage.System("sys"), AssistantChatMessage.User("draw a fox")],
            [new AssistantTool("generate_images", "Generate", """{"type":"object"}""")]);

        var reply = await client.CompleteChatAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:1234/v1/chat/completions", handler.Requests[0].RequestUri!.ToString());
        var body = JsonNode.Parse(handler.RequestBodies[0])!;
        Assert.Equal("gpt-local", body["model"]!.GetValue<string>());
        Assert.Equal("auto", body["tool_choice"]!.GetValue<string>());
        Assert.Equal("generate_images", body["tools"]![0]!["function"]!["name"]!.GetValue<string>());
        var call = Assert.Single(reply.ToolCalls);
        Assert.Equal("generate_images", call.Name);
        Assert.Equal("{\"prompt\":\"fox\"}", call.ArgumentsJson);
        Assert.Null(reply.Text);
    }

    [Fact]
    public async Task CheckKeyAsync_ModelsEndpoint_ReturnsStatus()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
        var client = CreateClient(handler, storedKey: null);

        var status = await client.CheckKeyAsync("new-key", TestContext.Current.CancellationToken);

        Assert.Equal(ApiKeyStatus.Valid, status);
        Assert.Equal("http://localhost:1234/v1/models", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal("Bearer new-key", handler.Requests[0].Headers.Authorization!.ToString());
    }

    /// <summary>Never answers; the client must stop by its own timeout.</summary>
    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}

using System.Net;
using System.Text.Json.Nodes;
using ImageGen.Core.Domain;
using ImageGen.Core.OpenRouter;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Tests.TestDoubles;

namespace ImageGen.Core.Tests.OpenRouter;

public class OpenRouterClientTests
{
    private const string ApiKey = "sk-or-test-1234";
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static OpenRouterClient CreateClient(StubHttpMessageHandler handler, string? storedKey = ApiKey)
    {
        var secrets = Substitute.For<ISecretStore>();
        secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns(storedKey);
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        var http = new HttpClient(handler) { BaseAddress = OpenRouterClient.BaseAddress };
        return new OpenRouterClient(http, secrets, time);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, ApiKeyStatus.Valid)]
    [InlineData(HttpStatusCode.Unauthorized, ApiKeyStatus.Invalid)]
    [InlineData(HttpStatusCode.Forbidden, ApiKeyStatus.Invalid)]
    [InlineData(HttpStatusCode.InternalServerError, ApiKeyStatus.NetworkError)]
    public async Task CheckKeyAsync_StatusCode_ReturnsExpectedStatus(HttpStatusCode code, ApiKeyStatus expected)
    {
        var handler = StubHttpMessageHandler.Json(code, "{}");
        var client = CreateClient(handler, storedKey: null);

        var status = await client.CheckKeyAsync("new-key", TestContext.Current.CancellationToken);

        Assert.Equal(expected, status);
        Assert.Equal("https://openrouter.ai/api/v1/key", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal("Bearer new-key", handler.Requests[0].Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task CheckKeyAsync_NetworkFailure_ReturnsNetworkError()
    {
        var client = CreateClient(StubHttpMessageHandler.Throws(new HttpRequestException("down")));

        var status = await client.CheckKeyAsync("new-key", TestContext.Current.CancellationToken);

        Assert.Equal(ApiKeyStatus.NetworkError, status);
    }

    [Fact]
    public async Task ListImageModelsAsync_ValidResponse_MapsModels()
    {
        const string json = """
            {"data":[
              {"id":"black-forest-labs/flux.2-klein-4b","name":"FLUX.2 Klein 4B",
               "architecture":{"input_modalities":["text","image"],"output_modalities":["image"]},
               "supported_parameters":{"seed":{"type":"boolean"},"n":{"type":"range","min":1,"max":1}}},
              {"id":"openai/gpt-image-1","name":"GPT Image 1",
               "architecture":{"input_modalities":["text"],"output_modalities":["image"]},
               "supported_parameters":{"n":{"type":"range","min":1,"max":4}}}
            ]}
            """;
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var models = await client.ListImageModelsAsync(TestContext.Current.CancellationToken);

        Assert.Equal("https://openrouter.ai/api/v1/images/models", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal(2, models.Count);
        Assert.Equal("black-forest-labs/flux.2-klein-4b", models[0].Id);
        Assert.Equal("FLUX.2 Klein 4B", models[0].Name);
        Assert.True(models[0].SupportsSeed);
        Assert.True(models[0].SupportsImageInput);
        Assert.Equal(1, models[0].MaxImagesPerRequest);
        Assert.Equal(Now, models[0].FetchedAt);
        Assert.False(models[1].SupportsSeed);
        Assert.False(models[1].SupportsImageInput);
        Assert.Equal(4, models[1].MaxImagesPerRequest);
    }

    [Fact]
    public async Task GenerateImageAsync_WithSeed_SendsSeedAndReturnsImage()
    {
        const string json = """{"created":1,"data":[{"b64_json":"AQID","media_type":"image/jpeg"}],"usage":{"cost":0.014}}""";
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var result = await client.GenerateImageAsync(
            new OpenRouterImageRequest("m/x", "a red fox", 42),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("https://openrouter.ai/api/v1/images", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal($"Bearer {ApiKey}", handler.Requests[0].Headers.Authorization!.ToString());
        var body = JsonNode.Parse(handler.RequestBodies[0])!;
        Assert.Equal("m/x", body["model"]!.GetValue<string>());
        Assert.Equal("a red fox", body["prompt"]!.GetValue<string>());
        Assert.Equal(1, body["n"]!.GetValue<int>());
        Assert.Equal(42, body["seed"]!.GetValue<long>());
        Assert.Equal([1, 2, 3], result.Content);
        Assert.Equal("image/jpeg", result.MediaType);
        Assert.Equal(0.014m, result.CostUsd);
    }

    [Fact]
    public async Task GenerateImageAsync_WithoutSeedAndMediaType_OmitsSeedAndDefaultsToPng()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """{"data":[{"b64_json":"AQID"}]}""");
        var client = CreateClient(handler);

        var result = await client.GenerateImageAsync(
            new OpenRouterImageRequest("m/x", "p", null),
            TestContext.Current.CancellationToken);

        Assert.False(JsonNode.Parse(handler.RequestBodies[0])!.AsObject().ContainsKey("seed"));
        Assert.Equal("image/png", result.MediaType);
        Assert.Null(result.CostUsd);
    }

    [Fact]
    public async Task GenerateImageAsync_EmptyData_ThrowsEmptyResponse()
    {
        var client = CreateClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, """{"data":[]}"""));

        var ex = await Assert.ThrowsAsync<ProviderException>(() => client.GenerateImageAsync(
            new OpenRouterImageRequest("m/x", "p", null),
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.EmptyResponse, ex.Kind);
    }

    [Theory]
    [InlineData(400, """{"error":{"code":400,"message":"bad"}}""", ErrorKind.BadRequest)]
    [InlineData(401, """{"error":{"code":401,"message":"no auth"}}""", ErrorKind.InvalidKey)]
    [InlineData(402, """{"error":{"code":402,"message":"no credits"}}""", ErrorKind.InsufficientFunds)]
    [InlineData(403, """{"error":{"code":403,"message":"flagged","metadata":{"reasons":["violence"]}}}""", ErrorKind.Moderated)]
    [InlineData(403, """{"error":{"code":403,"message":"forbidden"}}""", ErrorKind.Forbidden)]
    [InlineData(408, """{"error":{"code":408,"message":"timeout"}}""", ErrorKind.Timeout)]
    [InlineData(429, """{"error":{"code":429,"message":"slow down"}}""", ErrorKind.RateLimited)]
    [InlineData(502, """{"error":{"code":502,"message":"down"}}""", ErrorKind.ModelUnavailable)]
    [InlineData(503, """{"error":{"code":503,"message":"no provider"}}""", ErrorKind.ModelUnavailable)]
    [InlineData(500, "not json", ErrorKind.Unknown)]
    public async Task GenerateImageAsync_ErrorResponse_ThrowsMappedKind(int status, string json, ErrorKind expected)
    {
        var client = CreateClient(StubHttpMessageHandler.Json((HttpStatusCode)status, json));

        var ex = await Assert.ThrowsAsync<ProviderException>(() => client.GenerateImageAsync(
            new OpenRouterImageRequest("m/x", "p", null),
            TestContext.Current.CancellationToken));

        Assert.Equal(expected, ex.Kind);
        Assert.Equal(status, ex.StatusCode);
    }

    [Fact]
    public async Task GenerateImageAsync_ErrorInsideOkResponse_Throws()
    {
        var client = CreateClient(StubHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """{"error":{"code":502,"message":"provider failed"}}"""));

        var ex = await Assert.ThrowsAsync<ProviderException>(() => client.GenerateImageAsync(
            new OpenRouterImageRequest("m/x", "p", null),
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.ModelUnavailable, ex.Kind);
        Assert.Equal("provider failed", ex.Message);
    }

    [Fact]
    public async Task GenerateImageAsync_NetworkFailure_ThrowsNetwork()
    {
        var client = CreateClient(StubHttpMessageHandler.Throws(new HttpRequestException("down")));

        var ex = await Assert.ThrowsAsync<ProviderException>(() => client.GenerateImageAsync(
            new OpenRouterImageRequest("m/x", "p", null),
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.Network, ex.Kind);
    }

    [Fact]
    public async Task GenerateImageAsync_NoStoredKey_ThrowsInvalidKeyWithoutRequest()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
        var client = CreateClient(handler, storedKey: null);

        var ex = await Assert.ThrowsAsync<ProviderException>(() => client.GenerateImageAsync(
            new OpenRouterImageRequest("m/x", "p", null),
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.InvalidKey, ex.Kind);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GenerateImageAsync_ErrorMessage_DoesNotContainKey()
    {
        var client = CreateClient(StubHttpMessageHandler.Json(
            HttpStatusCode.Unauthorized,
            """{"error":{"code":401,"message":"No auth credentials found"}}"""));

        var ex = await Assert.ThrowsAsync<ProviderException>(() => client.GenerateImageAsync(
            new OpenRouterImageRequest("m/x", "p", null),
            TestContext.Current.CancellationToken));

        Assert.DoesNotContain(ApiKey, ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteChatAsync_ToolCall_SendsToolsAndParsesCall()
    {
        const string json = """
            {"choices":[{"finish_reason":"tool_calls","message":{"role":"assistant","content":null,
              "tool_calls":[{"id":"c1","type":"function","function":{"name":"generate_images","arguments":"{\"prompt\":\"a fox\"}"}}]}}],
             "usage":{"cost":0}}
            """;
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, json);
        var client = CreateClient(handler);
        var request = new OpenRouterChatRequest(
            "assistant/model",
            [OpenRouterChatMessage.System("sys"), OpenRouterChatMessage.User("draw a fox")],
            [new OpenRouterTool("generate_images", "Generate", """{"type":"object","properties":{"prompt":{"type":"string"}}}""")]);

        var result = await client.CompleteChatAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", handler.Requests[0].RequestUri!.ToString());
        var body = JsonNode.Parse(handler.RequestBodies[0])!;
        Assert.Equal("assistant/model", body["model"]!.GetValue<string>());
        Assert.Equal("system", body["messages"]![0]!["role"]!.GetValue<string>());
        Assert.Equal("draw a fox", body["messages"]![1]!["content"]!.GetValue<string>());
        Assert.Equal("function", body["tools"]![0]!["type"]!.GetValue<string>());
        Assert.Equal("generate_images", body["tools"]![0]!["function"]!["name"]!.GetValue<string>());
        Assert.Equal("object", body["tools"]![0]!["function"]!["parameters"]!["type"]!.GetValue<string>());
        Assert.Equal("auto", body["tool_choice"]!.GetValue<string>());
        Assert.Null(result.Text);
        var call = Assert.Single(result.ToolCalls);
        Assert.Equal("generate_images", call.Name);
        Assert.Equal("""{"prompt":"a fox"}""", call.ArgumentsJson);
        Assert.Equal(0m, result.CostUsd);
    }

    [Fact]
    public async Task CompleteChatAsync_TextReply_ReturnsTextWithoutToolCalls()
    {
        const string json = """{"choices":[{"message":{"role":"assistant","content":"Which style?"}}]}""";
        var client = CreateClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, json));

        var result = await client.CompleteChatAsync(
            new OpenRouterChatRequest("m", [OpenRouterChatMessage.User("hi")], []),
            TestContext.Current.CancellationToken);

        Assert.Equal("Which style?", result.Text);
        Assert.Empty(result.ToolCalls);
    }

    [Fact]
    public async Task CompleteChatAsync_UserMessageWithImage_SendsContentParts()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """{"choices":[{"message":{"content":"ok"}}]}""");
        var client = CreateClient(handler);
        var message = new OpenRouterChatMessage("user", "look", ["data:image/png;base64,AQID"]);

        await client.CompleteChatAsync(new OpenRouterChatRequest("m", [message], []), TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(handler.RequestBodies[0])!;
        var content = body["messages"]![0]!["content"]!.AsArray();
        Assert.Equal("text", content[0]!["type"]!.GetValue<string>());
        Assert.Equal("look", content[0]!["text"]!.GetValue<string>());
        Assert.Equal("image_url", content[1]!["type"]!.GetValue<string>());
        Assert.Equal("data:image/png;base64,AQID", content[1]!["image_url"]!["url"]!.GetValue<string>());
        Assert.False(body.AsObject().ContainsKey("tools"));
    }
}

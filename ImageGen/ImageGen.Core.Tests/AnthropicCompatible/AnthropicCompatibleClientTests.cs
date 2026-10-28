using System.Net;
using System.Text.Json.Nodes;
using ImageGen.Core.AnthropicCompatible;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Tests.TestDoubles;

namespace ImageGen.Core.Tests.AnthropicCompatible;

public class AnthropicCompatibleClientTests
{
    private const string Key = "sk-ant-5678";
    private const string Json = """
        {"providers":[{"id":"claude","kind":"anthropicCompatible","displayName":"Claude",
          "baseAddress":"https://api.anthropic.com/v1/","timeoutSeconds":30}]}
        """;

    private static AnthropicCompatibleClient CreateClient(StubHttpMessageHandler handler, string? storedKey = Key)
    {
        var secrets = Substitute.For<ISecretStore>();
        secrets.GetAsync("provider.claude.api_key", Arg.Any<CancellationToken>()).Returns(storedKey);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("claude").Returns(new HttpClient(handler));
        var config = ProviderConfigLoader.Build(Json, null).Get("claude");
        return new AnthropicCompatibleClient(config, factory, secrets, Substitute.For<TimeProvider>());
    }

    [Fact]
    public async Task CompleteChatAsync_SendsMessagesWithKeyHeadersSystemAndTools()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """{"content":[{"type":"text","text":"Hi"}]}""");
        var client = CreateClient(handler);
        var request = new AssistantChatRequest(
            "claude-model",
            [AssistantChatMessage.System("sys A"), AssistantChatMessage.System("sys B"),
             AssistantChatMessage.User("hello"), AssistantChatMessage.Assistant("earlier")],
            [new AssistantTool("generate_images", "Generate", """{"type":"object","properties":{"prompt":{"type":"string"}}}""")]);

        var reply = await client.CompleteChatAsync(request, TestContext.Current.CancellationToken);

        var sent = handler.Requests[0];
        Assert.Equal("https://api.anthropic.com/v1/messages", sent.RequestUri!.ToString());
        Assert.Equal(Key, sent.Headers.GetValues("x-api-key").Single());
        Assert.Equal(AnthropicCompatibleClient.ApiVersion, sent.Headers.GetValues("anthropic-version").Single());
        Assert.False(sent.Headers.Contains("Authorization"));
        var body = JsonNode.Parse(handler.RequestBodies[0])!;
        Assert.Equal("claude-model", body["model"]!.GetValue<string>());
        Assert.Equal(AnthropicCompatibleClient.MaxTokens, body["max_tokens"]!.GetValue<int>());
        Assert.Equal("sys A\n\nsys B", body["system"]!.GetValue<string>());
        var messages = body["messages"]!.AsArray();
        Assert.Equal(["user", "assistant"], messages.Select(m => m!["role"]!.GetValue<string>()));
        Assert.Equal("hello", messages[0]!["content"]!.GetValue<string>());
        Assert.Equal("generate_images", body["tools"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("object", body["tools"]![0]!["input_schema"]!["type"]!.GetValue<string>());
        Assert.Equal("Hi", reply.Text);
        Assert.Empty(reply.ToolCalls);
        Assert.Null(reply.CostUsd);
    }

    [Fact]
    public async Task CompleteChatAsync_ToolUseReply_ReturnsTextAndToolCall()
    {
        const string json = """
            {"content":[
              {"type":"text","text":"Here"},
              {"type":"tool_use","id":"t1","name":"generate_images","input":{"prompt":"fox"}}]}
            """;
        var client = CreateClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, json));

        var reply = await client.CompleteChatAsync(
            new AssistantChatRequest("m", [AssistantChatMessage.User("draw")], []),
            TestContext.Current.CancellationToken);

        Assert.Equal("Here", reply.Text);
        var call = Assert.Single(reply.ToolCalls);
        Assert.Equal("generate_images", call.Name);
        Assert.Equal("{\"prompt\":\"fox\"}", call.ArgumentsJson);
    }

    [Fact]
    public async Task CompleteChatAsync_ImageMessage_SendsBase64ImageBeforeText()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """{"content":[{"type":"text","text":"ok"}]}""");
        var client = CreateClient(handler);
        var message = new AssistantChatMessage("user", "look", ["data:image/png;base64,AQID"]);

        await client.CompleteChatAsync(new AssistantChatRequest("m", [message], []), TestContext.Current.CancellationToken);

        var content = JsonNode.Parse(handler.RequestBodies[0])!["messages"]![0]!["content"]!.AsArray();
        Assert.Equal("image", content[0]!["type"]!.GetValue<string>());
        Assert.Equal("image/png", content[0]!["source"]!["media_type"]!.GetValue<string>());
        Assert.Equal("base64", content[0]!["source"]!["type"]!.GetValue<string>());
        Assert.Equal("AQID", content[0]!["source"]!["data"]!.GetValue<string>());
        Assert.Equal("text", content[1]!["type"]!.GetValue<string>());
        Assert.Equal("look", content[1]!["text"]!.GetValue<string>());
    }

    [Fact]
    public async Task CompleteChatAsync_ImageNotDataUrl_ThrowsBadRequestWithoutRequest()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
        var client = CreateClient(handler);
        var message = new AssistantChatMessage("user", "look", ["https://x.test/a.png"]);

        var ex = await Assert.ThrowsAsync<ProviderException>(() =>
            client.CompleteChatAsync(new AssistantChatRequest("m", [message], []), TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.BadRequest, ex.Kind);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CompleteChatAsync_AuthenticationError_MapsToInvalidKeyWithProviderMessage()
    {
        const string json = """{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}""";
        var client = CreateClient(StubHttpMessageHandler.Json(HttpStatusCode.Unauthorized, json));

        var ex = await Assert.ThrowsAsync<ProviderException>(() =>
            client.CompleteChatAsync(new AssistantChatRequest("m", [AssistantChatMessage.User("x")], []), TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.InvalidKey, ex.Kind);
        Assert.Equal("invalid x-api-key", ex.Message);
    }

    [Fact]
    public async Task CheckKeyAsync_ModelsEndpointOk_ReturnsValid()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
        var client = CreateClient(handler, storedKey: null);

        var status = await client.CheckKeyAsync("new-key", TestContext.Current.CancellationToken);

        Assert.Equal(ApiKeyStatus.Valid, status);
        Assert.Equal("new-key", handler.Requests[0].Headers.GetValues("x-api-key").Single());
    }
}

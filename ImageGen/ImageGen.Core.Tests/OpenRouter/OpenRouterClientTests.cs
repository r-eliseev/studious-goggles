using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.OpenRouter;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Tests.TestDoubles;

namespace ImageGen.Core.Tests.OpenRouter;

public class OpenRouterClientTests
{
    private const string ApiKey = "sk-or-test-1234";
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly Uri OpenRouterAddress = new("https://openrouter.ai/api/v1/");
    private const string FluxModelId = "black-forest-labs/flux.2-klein-4b";
    private const string QwenModelId = "qwen/qwen-image-2.1-turbo";
    private const string FluxPriceJson = """
        {"endpoints":[{"pricing":[{"billable":"output_image","unit":"megapixel","cost_usd":0.014}]}]}
        """;
    private const string QwenPriceJson = """
        {"endpoints":[{"pricing":[
          {"billable":"input_image","unit":"image","cost_usd":0},
          {"billable":"output_image","unit":"image","cost_usd":0.0184,"variant":"1k"},
          {"billable":"output_image","unit":"image","cost_usd":0.0184,"variant":"2k"}
        ]}]}
        """;

    private static OpenRouterClient CreateClient(StubHttpMessageHandler handler, string? storedKey = ApiKey)
    {
        var secrets = Substitute.For<ISecretStore>();
        secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns(storedKey);
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(ProviderIds.OpenRouter).Returns(new HttpClient(handler));
        var config = ProviderConfigLoader.Build(ProviderConfigLoader.ReadBuiltInJson(), null).Get(ProviderIds.OpenRouter);
        return new OpenRouterClient(config, factory, secrets, time);
    }

    private static StubHttpMessageHandler PriceCatalogHandler() => new(request => request.RequestUri!.AbsolutePath switch
    {
        "/api/v1/images/models" => JsonResponse(HttpStatusCode.OK, ModelsJson(FluxModelId, QwenModelId)),
        "/api/v1/images/models/black-forest-labs/flux.2-klein-4b/endpoints" => JsonResponse(HttpStatusCode.OK, FluxPriceJson),
        "/api/v1/images/models/qwen/qwen-image-2.1-turbo/endpoints" => JsonResponse(HttpStatusCode.OK, QwenPriceJson),
        _ => JsonResponse(HttpStatusCode.NotFound, "{}"),
    });

    private static StubHttpMessageHandler SingleModelPriceHandler(string modelId, string endpointsJson) =>
        new(request => request.RequestUri!.AbsolutePath == "/api/v1/images/models"
            ? JsonResponse(HttpStatusCode.OK, ModelsJson(modelId))
            : JsonResponse(HttpStatusCode.OK, endpointsJson));

    private static string ModelsJson(params string[] ids)
    {
        var data = new JsonArray();
        foreach (var id in ids)
        {
            data.Add(new JsonObject { ["id"] = id, ["name"] = id });
        }

        return new JsonObject { ["data"] = data }.ToJsonString();
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

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
    public async Task ListImageModelsAsync_Endpoints_ParsesPrices()
    {
        var handler = PriceCatalogHandler();
        var client = CreateClient(handler);

        var models = await client.ListImageModelsAsync(TestContext.Current.CancellationToken);

        var fluxPrice = Assert.Single(models[0].Prices);
        Assert.Equal(new ImageModelPrice("output_image", ImagePriceUnit.Megapixel, 0.014m, null), fluxPrice);
        Assert.Collection(models[1].Prices,
            price => Assert.Equal(new ImageModelPrice("input_image", ImagePriceUnit.Image, 0m, null), price),
            price => Assert.Equal(new ImageModelPrice("output_image", ImagePriceUnit.Image, 0.0184m, "1k"), price),
            price => Assert.Equal(new ImageModelPrice("output_image", ImagePriceUnit.Image, 0.0184m, "2k"), price));
    }

    [Fact]
    public async Task ListImageModelsAsync_EndpointsAddress_KeepsSlashInId()
    {
        var handler = PriceCatalogHandler();
        var client = CreateClient(handler);

        await client.ListImageModelsAsync(TestContext.Current.CancellationToken);

        Assert.Equal("https://openrouter.ai/api/v1/images/models", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains(handler.Requests, request => request.RequestUri!.ToString()
            == "https://openrouter.ai/api/v1/images/models/black-forest-labs/flux.2-klein-4b/endpoints");
    }

    [Fact]
    public async Task ListImageModelsAsync_TokenUnit_ParsesPerToken()
    {
        const string endpoints = """{"endpoints":[{"pricing":[{"billable":"output_image","unit":"token","cost_usd":0.00004}]}]}""";
        var handler = SingleModelPriceHandler("openai/gpt-image-1", endpoints);
        var client = CreateClient(handler);

        var model = Assert.Single(await client.ListImageModelsAsync(TestContext.Current.CancellationToken));

        Assert.Equal(new ImageModelPrice("output_image", ImagePriceUnit.Token, 0.00004m, null), Assert.Single(model.Prices));
    }

    [Fact]
    public async Task ListImageModelsAsync_NoEndpoints_EmptyPrices()
    {
        var handler = SingleModelPriceHandler("meta/muse-image", """{"endpoints":[]}""");
        var client = CreateClient(handler);

        var model = Assert.Single(await client.ListImageModelsAsync(TestContext.Current.CancellationToken));

        Assert.Empty(model.Prices);
    }

    [Fact]
    public async Task ListImageModelsAsync_EndpointsFail_OtherModelsKeepPrices()
    {
        const string goodId = "vendor/good";
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/v1/images/models" => JsonResponse(HttpStatusCode.OK, ModelsJson("vendor/failing", goodId)),
            "/api/v1/images/models/vendor/failing/endpoints" => JsonResponse(HttpStatusCode.InternalServerError, "{}"),
            "/api/v1/images/models/vendor/good/endpoints" => JsonResponse(HttpStatusCode.OK, FluxPriceJson),
            _ => JsonResponse(HttpStatusCode.NotFound, "{}"),
        });
        var client = CreateClient(handler);

        var models = await client.ListImageModelsAsync(TestContext.Current.CancellationToken);

        Assert.Empty(models[0].Prices);
        Assert.NotEmpty(models[1].Prices);
    }

    [Fact]
    public async Task ListImageModelsAsync_UnknownUnit_SkipsEntry()
    {
        const string endpoints = """{"endpoints":[{"pricing":[{"billable":"output_image","unit":"image","cost_usd":0.02},{"billable":"output_image","unit":"second","cost_usd":1}]}]}""";
        var handler = SingleModelPriceHandler("vendor/model", endpoints);
        var client = CreateClient(handler);

        var model = Assert.Single(await client.ListImageModelsAsync(TestContext.Current.CancellationToken));

        Assert.Equal(new ImageModelPrice("output_image", ImagePriceUnit.Image, 0.02m, null), Assert.Single(model.Prices));
    }

    [Fact]
    public async Task ListImageModelsAsync_InvalidEndpointAndPricingTypes_SkipsInvalidEntries()
    {
        const string endpoints = """{"endpoints":[null,{"pricing":"bad"},{"pricing":[{"billable":1,"unit":"image","cost_usd":2},{"billable":"output_image","unit":"image","cost_usd":-1},{"billable":"output_image","unit":"image","cost_usd":0.03}]}]}""";
        var handler = SingleModelPriceHandler("vendor/model", endpoints);
        var client = CreateClient(handler);

        var model = Assert.Single(await client.ListImageModelsAsync(TestContext.Current.CancellationToken));

        Assert.Equal(new ImageModelPrice("output_image", ImagePriceUnit.Image, 0.03m, null), Assert.Single(model.Prices));
    }

    [Fact]
    public async Task ListImageModelsAsync_InvalidEndpointsRoot_ReturnsEmptyPrices()
    {
        var handler = SingleModelPriceHandler("vendor/model", """{"endpoints":"bad"}""");
        var client = CreateClient(handler);

        var model = Assert.Single(await client.ListImageModelsAsync(TestContext.Current.CancellationToken));

        Assert.Empty(model.Prices);
    }

    [Fact]
    public async Task ListImageModelsAsync_Cancelled_Throws()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
        var client = CreateClient(handler);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.ListImageModelsAsync(cancellation.Token));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GenerateImageAsync_WithSeed_SendsSeedAndReturnsImage()
    {
        const string json = """{"created":1,"data":[{"b64_json":"AQID","media_type":"image/jpeg"}],"usage":{"cost":0.014}}""";
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var result = await client.GenerateAsync(
            new ImageGenerationRequest("m/x", "a red fox", 42),
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

        var result = await client.GenerateAsync(
            new ImageGenerationRequest("m/x", "p", null),
            TestContext.Current.CancellationToken);

        Assert.False(JsonNode.Parse(handler.RequestBodies[0])!.AsObject().ContainsKey("seed"));
        Assert.Equal("image/png", result.MediaType);
        Assert.Null(result.CostUsd);
    }

    [Fact]
    public async Task ListImageModelsAsync_EnumRangeAndBooleanParameters_MapsCatalog()
    {
        const string json = """
            {"data":[{"id":"m/x","name":"X",
              "supported_parameters":{
                "aspect_ratio":{"type":"enum","values":["1:1","3:2","auto"]},
                "resolution":{"type":"enum","values":["1K","2K"]},
                "n":{"type":"range","min":1,"max":4},
                "seed":{"type":"boolean"}}}]}
            """;
        var client = CreateClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, json));

        var models = await client.ListImageModelsAsync(TestContext.Current.CancellationToken);

        var parameters = models[0].Parameters!;
        Assert.Equal(4, parameters.Count);
        var aspect = parameters.Single(p => p.Name == "aspect_ratio");
        Assert.Equal(ParameterKind.Enum, aspect.Kind);
        Assert.Equal(["1:1", "3:2", "auto"], aspect.Values);
        var count = parameters.Single(p => p.Name == "n");
        Assert.Equal(ParameterKind.Range, count.Kind);
        Assert.Equal(1, count.Min);
        Assert.Equal(4, count.Max);
        Assert.Equal(ParameterKind.Boolean, parameters.Single(p => p.Name == "seed").Kind);
    }

    [Fact]
    public async Task ListImageModelsAsync_UnknownParameterType_SkipsOnlyThatParameter()
    {
        const string json = """
            {"data":[{"id":"m/x","name":"X",
              "supported_parameters":{"size":{"type":"object"},"flag":true,"output_format":{"type":"enum","values":["png"]}}}]}
            """;
        var client = CreateClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, json));

        var models = await client.ListImageModelsAsync(TestContext.Current.CancellationToken);

        Assert.Equal("output_format", Assert.Single(models[0].Parameters!).Name);
    }

    [Fact]
    public async Task ListImageModelsAsync_NoSupportedParameters_EmptyListNotNull()
    {
        const string json = """{"data":[{"id":"m/x","name":"X"}]}""";
        var client = CreateClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, json));

        var models = await client.ListImageModelsAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(models[0].Parameters);
        Assert.Empty(models[0].Parameters!);
    }

    [Fact]
    public async Task GenerateImageAsync_PlannedParameters_SentUnderCatalogNames()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """{"data":[{"b64_json":"AQID"}]}""");
        var client = CreateClient(handler);

        await client.GenerateAsync(
            new ImageGenerationRequest("m/x", "p", null, AspectRatio: "3:2", Resolution: "1K", OutputFormat: "png", Quality: "high"),
            TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(handler.RequestBodies[0])!;
        Assert.Equal("3:2", body["aspect_ratio"]!.GetValue<string>());
        Assert.Equal("1K", body["resolution"]!.GetValue<string>());
        Assert.Equal("png", body["output_format"]!.GetValue<string>());
        Assert.Equal("high", body["quality"]!.GetValue<string>());
    }

    [Fact]
    public async Task GenerateImageAsync_NoPlannedParameters_NoParameterFieldsSent()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """{"data":[{"b64_json":"AQID"}]}""");
        var client = CreateClient(handler);

        await client.GenerateAsync(new ImageGenerationRequest("m/x", "p", null), TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(handler.RequestBodies[0])!.AsObject();
        Assert.False(body.ContainsKey("aspect_ratio"));
        Assert.False(body.ContainsKey("resolution"));
        Assert.False(body.ContainsKey("output_format"));
        Assert.False(body.ContainsKey("quality"));
    }

    [Fact]
    public async Task ListChatModelsAsync_ReadsInputModalitiesAndContextLength()
    {
        const string json = """
            {"data":[
              {"id":"vendor/vision","name":"Vision","context_length":262144,
               "architecture":{"input_modalities":["text","image"],"output_modalities":["text"]}},
              {"id":"vendor/text","name":"Text","context_length":8000,
               "architecture":{"input_modalities":["text"],"output_modalities":["text"]}},
              {"name":"No id","architecture":{"input_modalities":["image"]}}
            ]}
            """;
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var models = await client.ListChatModelsAsync(TestContext.Current.CancellationToken);

        Assert.Equal("https://openrouter.ai/api/v1/models", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal(2, models.Count);
        Assert.True(models[0].SupportsImageInput);
        Assert.Equal(262144, models[0].ContextLength);
        Assert.False(models[1].SupportsImageInput);
    }

    [Fact]
    public async Task ListChatModelsAsync_ReadsToolsAndPricePerMillionTokens()
    {
        const string json = """
            {"data":[
              {"id":"anthropic/claude-haiku-5.5","name":"Claude Haiku 5.5",
               "supported_parameters":["max_tokens","tools","tool_choice","seed"],
               "pricing":{"prompt":"0.0000001","completion":"0.0000005"},
               "architecture":{"input_modalities":["text","image"],"output_modalities":["text"]}},
              {"id":"google/gemma-4-31b-it:free","name":"Gemma",
               "supported_parameters":["max_tokens"],
               "pricing":{"prompt":"0","completion":"0"},
               "architecture":{"input_modalities":["text"],"output_modalities":["text"]}}
            ]}
            """;
        var client = CreateClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, json));

        var models = await client.ListChatModelsAsync(TestContext.Current.CancellationToken);

        Assert.True(models[0].SupportsTools);
        Assert.Equal(0.1m, models[0].PromptPricePerMillion);
        Assert.Equal(0.5m, models[0].CompletionPricePerMillion);
        Assert.False(models[0].IsFree);
        Assert.False(models[1].SupportsTools);
        Assert.True(models[1].IsFree);
    }

    [Fact]
    public async Task GenerateImageAsync_EmptyData_ThrowsEmptyResponse()
    {
        var client = CreateClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, """{"data":[]}"""));

        var ex = await Assert.ThrowsAsync<ProviderException>(() => client.GenerateAsync(
            new ImageGenerationRequest("m/x", "p", null),
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

        var ex = await Assert.ThrowsAsync<ProviderException>(() => client.GenerateAsync(
            new ImageGenerationRequest("m/x", "p", null),
            TestContext.Current.CancellationToken));

        Assert.Equal(expected, ex.Kind);
    }

    [Fact]
    public async Task GenerateImageAsync_ErrorInsideOkResponse_Throws()
    {
        var client = CreateClient(StubHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """{"error":{"code":502,"message":"provider failed"}}"""));

        var ex = await Assert.ThrowsAsync<ProviderException>(() => client.GenerateAsync(
            new ImageGenerationRequest("m/x", "p", null),
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.ModelUnavailable, ex.Kind);
        Assert.Equal("provider failed", ex.Message);
    }

    [Fact]
    public async Task GenerateImageAsync_NetworkFailure_ThrowsNetwork()
    {
        var client = CreateClient(StubHttpMessageHandler.Throws(new HttpRequestException("down")));

        var ex = await Assert.ThrowsAsync<ProviderException>(() => client.GenerateAsync(
            new ImageGenerationRequest("m/x", "p", null),
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.Network, ex.Kind);
    }

    [Fact]
    public async Task GenerateImageAsync_NoStoredKey_ThrowsInvalidKeyWithoutRequest()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
        var client = CreateClient(handler, storedKey: null);

        var ex = await Assert.ThrowsAsync<ProviderException>(() => client.GenerateAsync(
            new ImageGenerationRequest("m/x", "p", null),
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

        var ex = await Assert.ThrowsAsync<ProviderException>(() => client.GenerateAsync(
            new ImageGenerationRequest("m/x", "p", null),
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
        var request = new AssistantChatRequest(
            "assistant/model",
            [AssistantChatMessage.System("sys"), AssistantChatMessage.User("draw a fox")],
            [new AssistantTool("generate_images", "Generate", """{"type":"object","properties":{"prompt":{"type":"string"}}}""")]);

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
            new AssistantChatRequest("m", [AssistantChatMessage.User("hi")], []),
            TestContext.Current.CancellationToken);

        Assert.Equal("Which style?", result.Text);
        Assert.Empty(result.ToolCalls);
    }

    [Fact]
    public async Task CompleteChatAsync_UserMessageWithImage_SendsContentParts()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """{"choices":[{"message":{"content":"ok"}}]}""");
        var client = CreateClient(handler);
        var message = new AssistantChatMessage("user", "look", ["data:image/png;base64,AQID"]);

        await client.CompleteChatAsync(new AssistantChatRequest("m", [message], []), TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(handler.RequestBodies[0])!;
        var content = body["messages"]![0]!["content"]!.AsArray();
        Assert.Equal("text", content[0]!["type"]!.GetValue<string>());
        Assert.Equal("look", content[0]!["text"]!.GetValue<string>());
        Assert.Equal("image_url", content[1]!["type"]!.GetValue<string>());
        Assert.Equal("data:image/png;base64,AQID", content[1]!["image_url"]!["url"]!.GetValue<string>());
        Assert.False(body.AsObject().ContainsKey("tools"));
    }

    [Fact]
    public async Task CompleteChatAsync_ReadsPromptTokens()
    {
        const string json = """{"choices":[{"message":{"content":"ok"}}],"usage":{"prompt_tokens":1234,"cost":0.01}}""";
        var client = CreateClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, json));

        var result = await client.CompleteChatAsync(
            new AssistantChatRequest("m", [AssistantChatMessage.User("hi")], []),
            TestContext.Current.CancellationToken);

        Assert.Equal(1234, result.PromptTokens);
    }
}

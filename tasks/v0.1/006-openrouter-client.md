---
id: 006
status: planned
assignee:
claimed:
created: 2026-10-27
---

# Ядро: HTTP-клиент OpenRouter

## Описание
Клиент `OpenRouterClient` для четырёх вызовов OpenRouter: проверка ключа (`GET key`), список моделей картинок (`GET images/models`), генерация картинки (`POST images`), chat completions с инструментами и картинками на входе (`POST chat/completions`). Ошибки приводятся к `ProviderException` с `ErrorKind`. Плюс подмена HTTP для тестов `StubHttpMessageHandler`.

Зависит от: 002.

## Критерии готовности
- Файлы из плана созданы с содержимым как в плане.
- Все тесты `ImageGen.Core.Tests` проходят.
- `dotnet format` для `ImageGen.Core` и `ImageGen.Core.Tests` проходит.
- Реальные ключи и сетевые запросы в тестах не используются.

## Анализ
Формат API по документации OpenRouter на 2026-10-27 (`docs/specs/image-client.md`, раздел про API; страницы `openrouter.ai/docs`):
- Базовый адрес `https://openrouter.ai/api/v1/`, заголовок `Authorization: Bearer <key>`.
- `POST images`: тело `{"model","prompt","n","seed"}`, ответ `data[].b64_json`, `data[].media_type` (может отсутствовать), `usage.cost` (USD, может отсутствовать). Бесплатных моделей картинок нет.
- `GET images/models`: `data[]` с `id`, `name`, `architecture.input_modalities`, `supported_parameters` (ключ есть — параметр поддерживается; у `n` есть `max`).
- `POST chat/completions`: формат OpenAI; инструменты `tools[].function{name,description,parameters}`, `tool_choice: "auto"`; ответ `choices[0].message.content` (строка или null) и `choices[0].message.tool_calls[].function{name, arguments}` (`arguments` — строка JSON).
- Ошибки: тело `{"error":{"code","message","metadata"}}`; `code` равен HTTP-статусу. 403 с `metadata.reasons` — модерация. Ответ 200 тоже может содержать только `error`.
- `GET key`: 200 — ключ работает, 401 — неверный.
- JSON разбирается через `JsonNode`, без рефлексии (trimming на iOS).
- Код проверен сборкой и тестами 2026-10-27 в пробной копии решения. Живые запросы к OpenRouter не выполнялись: ключа при подготовке не было. Первая ручная проверка — задача 016.

## План
1. Создать файлы в `ImageGen/ImageGen.Core/OpenRouter/` (содержимое дословно):

   `ImageGen/ImageGen.Core/OpenRouter/OpenRouterImageRequest.cs`
   ```csharp
   namespace ImageGen.Core.OpenRouter;

   /// <param name="Seed">Null when the seed must not be sent.</param>
   public sealed record OpenRouterImageRequest(string Model, string Prompt, long? Seed);
   ```

   `ImageGen/ImageGen.Core/OpenRouter/OpenRouterImageResult.cs`
   ```csharp
   namespace ImageGen.Core.OpenRouter;

   public sealed record OpenRouterImageResult(byte[] Content, string MediaType, decimal? CostUsd);
   ```

   `ImageGen/ImageGen.Core/OpenRouter/OpenRouterChatMessage.cs`
   ```csharp
   namespace ImageGen.Core.OpenRouter;

   /// <param name="Role">"system", "user" or "assistant".</param>
   /// <param name="ImageDataUrls">Images as data URLs (<c>data:image/png;base64,...</c>); only for "user" messages.</param>
   public sealed record OpenRouterChatMessage(string Role, string Text, IReadOnlyList<string> ImageDataUrls)
   {
       public const string SystemRole = "system";
       public const string UserRole = "user";
       public const string AssistantRole = "assistant";

       public static OpenRouterChatMessage System(string text) => new(SystemRole, text, []);

       public static OpenRouterChatMessage User(string text) => new(UserRole, text, []);

       public static OpenRouterChatMessage Assistant(string text) => new(AssistantRole, text, []);
   }
   ```

   `ImageGen/ImageGen.Core/OpenRouter/OpenRouterTool.cs`
   ```csharp
   namespace ImageGen.Core.OpenRouter;

   /// <param name="ParametersJsonSchema">JSON Schema of the tool arguments as a JSON string.</param>
   public sealed record OpenRouterTool(string Name, string Description, string ParametersJsonSchema);
   ```

   `ImageGen/ImageGen.Core/OpenRouter/OpenRouterChatRequest.cs`
   ```csharp
   namespace ImageGen.Core.OpenRouter;

   public sealed record OpenRouterChatRequest(
       string Model,
       IReadOnlyList<OpenRouterChatMessage> Messages,
       IReadOnlyList<OpenRouterTool> Tools);
   ```

   `ImageGen/ImageGen.Core/OpenRouter/OpenRouterToolCall.cs`
   ```csharp
   namespace ImageGen.Core.OpenRouter;

   /// <param name="ArgumentsJson">Tool arguments as returned by the model (a JSON string).</param>
   public sealed record OpenRouterToolCall(string Name, string ArgumentsJson);
   ```

   `ImageGen/ImageGen.Core/OpenRouter/OpenRouterChatResult.cs`
   ```csharp
   namespace ImageGen.Core.OpenRouter;

   /// <param name="Text">Assistant text, or null when the model returned only tool calls.</param>
   public sealed record OpenRouterChatResult(string? Text, IReadOnlyList<OpenRouterToolCall> ToolCalls, decimal? CostUsd);
   ```

   `ImageGen/ImageGen.Core/OpenRouter/IOpenRouterClient.cs`
   ```csharp
   using ImageGen.Core.Domain;

   namespace ImageGen.Core.OpenRouter;

   /// <summary>OpenRouter HTTP API. All methods except <see cref="CheckKeyAsync"/> read the key from the secret store.</summary>
   public interface IOpenRouterClient
   {
       /// <summary>Checks the given key with <c>GET key</c>. Never throws for HTTP or network errors.</summary>
       Task<ApiKeyStatus> CheckKeyAsync(string apiKey, CancellationToken cancellationToken);

       /// <summary>Loads image models with <c>GET images/models</c>.</summary>
       Task<IReadOnlyList<ImageModel>> ListImageModelsAsync(CancellationToken cancellationToken);

       /// <summary>Generates one image with <c>POST images</c>.</summary>
       Task<OpenRouterImageResult> GenerateImageAsync(OpenRouterImageRequest request, CancellationToken cancellationToken);

       /// <summary>Sends a chat completion with <c>POST chat/completions</c>.</summary>
       Task<OpenRouterChatResult> CompleteChatAsync(OpenRouterChatRequest request, CancellationToken cancellationToken);
   }
   ```

   `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`
   ```csharp
   using System.Globalization;
   using System.Net;
   using System.Net.Http.Headers;
   using System.Text;
   using System.Text.Json;
   using System.Text.Json.Nodes;
   using ImageGen.Core.Domain;
   using ImageGen.Core.Platform;
   using ImageGen.Core.Providers;

   namespace ImageGen.Core.OpenRouter;

   public sealed class OpenRouterClient(HttpClient httpClient, ISecretStore secretStore, TimeProvider timeProvider) : IOpenRouterClient
   {
       public static readonly Uri BaseAddress = new("https://openrouter.ai/api/v1/");
       public const string DefaultMediaType = "image/png";

       public async Task<ApiKeyStatus> CheckKeyAsync(string apiKey, CancellationToken cancellationToken)
       {
           ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
           using var request = new HttpRequestMessage(HttpMethod.Get, "key");
           request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
           try
           {
               using var response = await httpClient.SendAsync(request, cancellationToken);
               return response.StatusCode switch
               {
                   HttpStatusCode.OK => ApiKeyStatus.Valid,
                   HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ApiKeyStatus.Invalid,
                   _ => ApiKeyStatus.NetworkError,
               };
           }
           catch (HttpRequestException)
           {
               return ApiKeyStatus.NetworkError;
           }
           catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
           {
               return ApiKeyStatus.NetworkError;
           }
       }

       public async Task<IReadOnlyList<ImageModel>> ListImageModelsAsync(CancellationToken cancellationToken)
       {
           using var request = await CreateRequestAsync(HttpMethod.Get, "images/models", body: null, cancellationToken);
           var root = await SendAsync(request, cancellationToken);
           var fetchedAt = timeProvider.GetUtcNow();
           var result = new List<ImageModel>();
           if (root["data"] is not JsonArray data)
           {
               return result;
           }

           foreach (var item in data)
           {
               var id = item?["id"]?.GetValue<string>();
               if (string.IsNullOrWhiteSpace(id))
               {
                   continue;
               }

               var parameters = item!["supported_parameters"] as JsonObject;
               var inputModalities = item["architecture"]?["input_modalities"] as JsonArray;
               result.Add(new ImageModel
               {
                   Id = id,
                   Name = item["name"]?.GetValue<string>() ?? id,
                   SupportsSeed = parameters?.ContainsKey("seed") == true,
                   SupportsImageInput = inputModalities?.Any(m => m?.GetValue<string>() == "image") == true,
                   MaxImagesPerRequest = ReadMaxImages(parameters),
                   FetchedAt = fetchedAt,
               });
           }

           return result;
       }

       public async Task<OpenRouterImageResult> GenerateImageAsync(OpenRouterImageRequest request, CancellationToken cancellationToken)
       {
           ArgumentNullException.ThrowIfNull(request);
           var body = new JsonObject
           {
               ["model"] = request.Model,
               ["prompt"] = request.Prompt,
               ["n"] = 1,
           };
           if (request.Seed.HasValue)
           {
               body["seed"] = request.Seed.Value;
           }

           using var message = await CreateRequestAsync(HttpMethod.Post, "images", body, cancellationToken);
           var root = await SendAsync(message, cancellationToken);
           var first = (root["data"] as JsonArray)?.FirstOrDefault();
           var base64 = first?["b64_json"]?.GetValue<string>();
           if (string.IsNullOrEmpty(base64))
           {
               throw new ProviderException(ErrorKind.EmptyResponse, "OpenRouter returned no image.");
           }

           var mediaType = first!["media_type"]?.GetValue<string>();
           return new OpenRouterImageResult(
               Convert.FromBase64String(base64),
               string.IsNullOrWhiteSpace(mediaType) ? DefaultMediaType : mediaType,
               ReadCost(root));
       }

       public async Task<OpenRouterChatResult> CompleteChatAsync(OpenRouterChatRequest request, CancellationToken cancellationToken)
       {
           ArgumentNullException.ThrowIfNull(request);
           var messages = new JsonArray();
           foreach (var message in request.Messages)
           {
               messages.Add(BuildMessage(message));
           }

           var body = new JsonObject
           {
               ["model"] = request.Model,
               ["messages"] = messages,
           };
           if (request.Tools.Count > 0)
           {
               var tools = new JsonArray();
               foreach (var tool in request.Tools)
               {
                   tools.Add(new JsonObject
                   {
                       ["type"] = "function",
                       ["function"] = new JsonObject
                       {
                           ["name"] = tool.Name,
                           ["description"] = tool.Description,
                           ["parameters"] = JsonNode.Parse(tool.ParametersJsonSchema),
                       },
                   });
               }

               body["tools"] = tools;
               body["tool_choice"] = "auto";
           }

           using var httpRequest = await CreateRequestAsync(HttpMethod.Post, "chat/completions", body, cancellationToken);
           var root = await SendAsync(httpRequest, cancellationToken);
           var choiceMessage = (root["choices"] as JsonArray)?.FirstOrDefault()?["message"];
           if (choiceMessage is null)
           {
               throw new ProviderException(ErrorKind.EmptyResponse, "OpenRouter returned no choices.");
           }

           var text = choiceMessage["content"] is JsonValue content && content.TryGetValue<string>(out var value) ? value : null;
           var toolCalls = new List<OpenRouterToolCall>();
           if (choiceMessage["tool_calls"] is JsonArray calls)
           {
               foreach (var call in calls)
               {
                   var name = call?["function"]?["name"]?.GetValue<string>();
                   if (string.IsNullOrEmpty(name))
                   {
                       continue;
                   }

                   var arguments = call!["function"]!["arguments"];
                   var argumentsJson = arguments is JsonValue argumentsValue && argumentsValue.TryGetValue<string>(out var s)
                       ? s
                       : arguments?.ToJsonString() ?? "{}";
                   toolCalls.Add(new OpenRouterToolCall(name, argumentsJson));
               }
           }

           return new OpenRouterChatResult(text, toolCalls, ReadCost(root));
       }

       private static JsonObject BuildMessage(OpenRouterChatMessage message)
       {
           if (message.ImageDataUrls.Count == 0)
           {
               return new JsonObject { ["role"] = message.Role, ["content"] = message.Text };
           }

           var parts = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = message.Text } };
           foreach (var url in message.ImageDataUrls)
           {
               parts.Add(new JsonObject
               {
                   ["type"] = "image_url",
                   ["image_url"] = new JsonObject { ["url"] = url },
               });
           }

           return new JsonObject { ["role"] = message.Role, ["content"] = parts };
       }

       private async Task<HttpRequestMessage> CreateRequestAsync(
           HttpMethod method,
           string path,
           JsonObject? body,
           CancellationToken cancellationToken)
       {
           var apiKey = await secretStore.GetAsync(SecretNames.OpenRouterApiKey, cancellationToken);
           if (string.IsNullOrWhiteSpace(apiKey))
           {
               throw new ProviderException(ErrorKind.InvalidKey, "OpenRouter API key is not set.");
           }

           var request = new HttpRequestMessage(method, path);
           request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
           if (body is not null)
           {
               request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
           }

           return request;
       }

       private async Task<JsonNode> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
       {
           HttpResponseMessage response;
           try
           {
               response = await httpClient.SendAsync(request, cancellationToken);
           }
           catch (HttpRequestException ex)
           {
               throw new ProviderException(ErrorKind.Network, "Could not reach OpenRouter.", innerException: ex);
           }
           catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
           {
               throw new ProviderException(ErrorKind.Timeout, "OpenRouter did not respond in time.", innerException: ex);
           }

           using (response)
           {
               var text = await response.Content.ReadAsStringAsync(cancellationToken);
               JsonNode? root = null;
               try
               {
                   root = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
               }
               catch (JsonException)
               {
                   root = null;
               }

               var statusCode = (int)response.StatusCode;
               var error = root?["error"];
               if (!response.IsSuccessStatusCode || error is not null)
               {
                   var code = error?["code"] is JsonValue codeValue && codeValue.TryGetValue<int>(out var c) ? c : statusCode;
                   var message = error?["message"]?.GetValue<string>() ?? $"OpenRouter returned HTTP {statusCode}.";
                   var hasReasons = error?["metadata"]?["reasons"] is JsonArray;
                   throw new ProviderException(MapErrorKind(code, hasReasons), message, code);
               }

               return root ?? throw new ProviderException(ErrorKind.EmptyResponse, "OpenRouter returned an empty response.", statusCode);
           }
       }

       /// <summary>Maps an OpenRouter error code (equal to the HTTP status) to <see cref="ErrorKind"/>.</summary>
       public static ErrorKind MapErrorKind(int code, bool hasModerationReasons) => code switch
       {
           400 => ErrorKind.BadRequest,
           401 => ErrorKind.InvalidKey,
           402 => ErrorKind.InsufficientFunds,
           403 when hasModerationReasons => ErrorKind.Moderated,
           403 => ErrorKind.Forbidden,
           408 => ErrorKind.Timeout,
           429 => ErrorKind.RateLimited,
           502 or 503 => ErrorKind.ModelUnavailable,
           _ => ErrorKind.Unknown,
       };

       private static decimal? ReadCost(JsonNode root)
       {
           if (root["usage"]?["cost"] is not JsonValue cost)
           {
               return null;
           }

           if (cost.TryGetValue<decimal>(out var number))
           {
               return number;
           }

           return cost.TryGetValue<string>(out var s)
               && decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
               ? parsed
               : null;
       }

       private static int ReadMaxImages(JsonObject? parameters)
       {
           if (parameters?["n"]?["max"] is JsonValue max && max.TryGetValue<int>(out var value) && value > 0)
           {
               return value;
           }

           return 1;
       }
   }
   ```

2. Создать тестовые файлы (содержимое дословно):

   `ImageGen/ImageGen.Core.Tests/TestDoubles/StubHttpMessageHandler.cs`
   ```csharp
   using System.Net;
   using System.Text;

   namespace ImageGen.Core.Tests.TestDoubles;

   /// <summary>Returns a prepared response and records every request with its body.</summary>
   public sealed class StubHttpMessageHandler : HttpMessageHandler
   {
       private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

       public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
       {
           _respond = respond;
       }

       public List<HttpRequestMessage> Requests { get; } = [];

       public List<string> RequestBodies { get; } = [];

       public static StubHttpMessageHandler Json(HttpStatusCode statusCode, string json) =>
           new(_ => new HttpResponseMessage(statusCode)
           {
               Content = new StringContent(json, Encoding.UTF8, "application/json"),
           });

       public static StubHttpMessageHandler Throws(Exception exception) => new(_ => throw exception);

       protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
       {
           Requests.Add(request);
           RequestBodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
           return _respond(request);
       }
   }
   ```

   `ImageGen/ImageGen.Core.Tests/OpenRouter/OpenRouterClientTests.cs`
   ```csharp
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
   ```

Файлы: `ImageGen/ImageGen.Core/OpenRouter/*.cs` (кроме `OpenRouterImageGenerator.cs`, он в задаче 009), `ImageGen/ImageGen.Core.Tests/TestDoubles/StubHttpMessageHandler.cs`, `ImageGen/ImageGen.Core.Tests/OpenRouter/OpenRouterClientTests.cs`

### Проверка
```
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
scripts/check.sh
```

## Отчёт

## Комментарии

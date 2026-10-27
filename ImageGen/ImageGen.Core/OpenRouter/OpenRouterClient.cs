using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;

namespace ImageGen.Core.OpenRouter;

public sealed class OpenRouterClient(HttpClient httpClient, ISecretStore secretStore, TimeProvider timeProvider) : IModelProvider
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

    public async Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = new JsonObject
        {
            ["model"] = request.ModelId,
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
        return new ImageGenerationResult(
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
                throw new ProviderException(MapErrorKind(code, hasReasons), message);
            }

            return root ?? throw new ProviderException(ErrorKind.EmptyResponse, "OpenRouter returned an empty response.");
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

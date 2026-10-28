using System.Text.Json.Nodes;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;

namespace ImageGen.Core.AnthropicCompatible;

/// <summary>
/// Anthropic Messages API (<c>messages</c>) for the assistant chat only. No images: the provider is not
/// listed among image providers.
/// </summary>
public sealed class AnthropicCompatibleClient(
    ProviderConfig config,
    IHttpClientFactory httpClientFactory,
    ISecretStore secretStore,
    TimeProvider timeProvider)
    : ProviderClientBase(config, httpClientFactory, secretStore, timeProvider), IAssistantChatClient
{
    /// <summary>Version of the API the request format follows (sent in the <c>anthropic-version</c> header).</summary>
    public const string ApiVersion = "2023-06-01";

    /// <summary>Upper bound of the reply length; the API requires it.</summary>
    public const int MaxTokens = 4096;

    protected override void Authenticate(HttpRequestMessage request, string apiKey)
    {
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
    }

    public override Task<ApiKeyStatus> CheckKeyAsync(string apiKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        return ProbeKeyAsync(HttpMethod.Get, "models", apiKey, cancellationToken);
    }

    /// <summary>These APIs do not list input modalities, so no model is known to take images.</summary>
    public Task<IReadOnlyList<ImageGen.Core.Domain.Models.ChatModel>> ListChatModelsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ImageGen.Core.Domain.Models.ChatModel>>([]);

    public async Task<AssistantChatResult> CompleteChatAsync(AssistantChatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var httpRequest = await CreateAuthorizedRequestAsync(
            HttpMethod.Post, "messages", BuildBody(request), cancellationToken);
        var root = await SendJsonAsync(httpRequest, cancellationToken);
        return ReadReply(root);
    }

    /// <summary>
    /// Builds the Messages body. System messages go to the top-level <c>system</c> field; each image is a
    /// base64 block placed before the text of its message.
    /// </summary>
    internal JsonObject BuildBody(AssistantChatRequest request)
    {
        var system = new List<string>();
        var messages = new JsonArray();
        foreach (var message in request.Messages)
        {
            if (message.Role == AssistantChatMessage.SystemRole)
            {
                system.Add(message.Text);
                continue;
            }

            messages.Add(new JsonObject
            {
                ["role"] = message.Role,
                ["content"] = BuildContent(message),
            });
        }

        var body = new JsonObject
        {
            ["model"] = request.Model,
            ["max_tokens"] = MaxTokens,
            ["messages"] = messages,
        };
        if (system.Count > 0)
        {
            body["system"] = string.Join("\n\n", system);
        }

        if (request.Tools.Count > 0)
        {
            var tools = new JsonArray();
            foreach (var tool in request.Tools)
            {
                tools.Add(new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["input_schema"] = JsonNode.Parse(tool.ParametersJsonSchema),
                });
            }

            body["tools"] = tools;
        }

        return body;
    }

    private static JsonNode BuildContent(AssistantChatMessage message)
    {
        if (message.ImageDataUrls.Count == 0)
        {
            return message.Text;
        }

        var parts = new JsonArray();
        foreach (var url in message.ImageDataUrls)
        {
            var (mediaType, data) = SplitDataUrl(url);
            parts.Add(new JsonObject
            {
                ["type"] = "image",
                ["source"] = new JsonObject
                {
                    ["type"] = "base64",
                    ["media_type"] = mediaType,
                    ["data"] = data,
                },
            });
        }

        parts.Add(new JsonObject { ["type"] = "text", ["text"] = message.Text });
        return parts;
    }

    private static (string MediaType, string Data) SplitDataUrl(string url)
    {
        const string Marker = ";base64,";
        var index = url.IndexOf(Marker, StringComparison.Ordinal);
        if (!url.StartsWith("data:", StringComparison.Ordinal) || index < 0)
        {
            throw new ProviderException(ErrorKind.BadRequest, "An image must be a base64 data URL.");
        }

        return (url["data:".Length..index], url[(index + Marker.Length)..]);
    }

    private static AssistantChatResult ReadReply(JsonNode root)
    {
        var content = root["content"] as JsonArray;
        if (content is null)
        {
            throw new ProviderException(ErrorKind.EmptyResponse, "Anthropic returned no content.");
        }

        var texts = new List<string>();
        var toolCalls = new List<AssistantToolCall>();
        foreach (var block in content)
        {
            var type = block?["type"]?.GetValue<string>();
            if (type == "text" && block?["text"]?.GetValue<string>() is { } text)
            {
                texts.Add(text);
            }
            else if (type == "tool_use" && block?["name"]?.GetValue<string>() is { Length: > 0 } name)
            {
                toolCalls.Add(new AssistantToolCall(name, block!["input"]?.ToJsonString() ?? "{}"));
            }
        }

        return new AssistantChatResult(texts.Count == 0 ? null : string.Concat(texts), toolCalls, null);
    }
}

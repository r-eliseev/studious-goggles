using System.Text.Json.Nodes;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;

namespace ImageGen.Core.OpenAiCompatible;

/// <summary>
/// Any API in the OpenAI format: images through <c>images/generations</c> (base64 only), chat through
/// <c>chat/completions</c>. Its image models are listed in the config file, because the API does not describe them.
/// </summary>
public sealed class OpenAiCompatibleClient(
    ProviderConfig config,
    IHttpClientFactory httpClientFactory,
    ISecretStore secretStore,
    TimeProvider timeProvider)
    : ProviderClientBase(config, httpClientFactory, secretStore, timeProvider), IModelProvider, IAssistantChatClient, ISynchronousImageTransport
{
    protected override void Authenticate(HttpRequestMessage request, string apiKey) => AuthenticateBearer(request, apiKey);

    public override Task<ApiKeyStatus> CheckKeyAsync(string apiKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        return ProbeKeyAsync(HttpMethod.Get, "models", apiKey, cancellationToken);
    }

    public Task<IReadOnlyList<ImageModel>> ListImageModelsAsync(CancellationToken cancellationToken)
    {
        var fetchedAt = TimeProvider.GetUtcNow();
        IReadOnlyList<ImageModel> models = Config.ImageModels
            .Select(m => new ImageModel
            {
                ProviderId = Config.Id,
                Id = m.Id,
                Name = m.Name,
                SupportsSeed = m.SupportsSeed,
                SupportsImageInput = false,
                MaxImagesPerRequest = m.MaxImagesPerRequest,
                // The config does not list parameters, and this client does not send any: an empty list, not unknown.
                Parameters = [],
                FetchedAt = fetchedAt,
            })
            .ToList();
        return Task.FromResult(models);
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
        if (Config.ImageResponseFormat is { } responseFormat)
        {
            body["response_format"] = responseFormat;
        }

        if (Config.ImageOutputFormat is { } outputFormat)
        {
            body["output_format"] = outputFormat;
        }

        if (request.Seed.HasValue)
        {
            body["seed"] = request.Seed.Value;
        }

        using var message = await CreateAuthorizedRequestAsync(HttpMethod.Post, "images/generations", body, cancellationToken);
        message.Options.Set(ProviderResponseLimits.BodyKind, ProviderResponseBodyKind.GeneratedImageJson);
        var root = await SendJsonAsync(message, cancellationToken);
        var first = (root["data"] as JsonArray)?.FirstOrDefault();
        var base64 = first?["b64_json"]?.GetValue<string>();
        if (string.IsNullOrEmpty(base64))
        {
            // Only base64 answers are supported; an answer with a URL would need a second download (not built yet).
            throw new ProviderException(ErrorKind.EmptyResponse, $"{Config.DisplayName} returned no base64 image.");
        }

        try
        {
            var mediaType = Config.ImageOutputFormat switch
            {
                "png" => "image/png",
                "jpeg" => "image/jpeg",
                _ => DefaultMediaType,
            };
            return new ImageGenerationResult(DecodeImage(base64), mediaType, null);
        }
        catch (ProviderResponseTooLargeException ex)
        {
            throw new ProviderException(ErrorKind.Unknown, "Provider response exceeded the configured size limit.", innerException: ex);
        }
    }

    private static byte[] DecodeImage(string base64)
    {
        var maximumEncodedLength = ((ProviderResponseLimits.Image + 2) / 3) * 4;
        if (base64.Length > maximumEncodedLength)
        {
            throw new ProviderResponseTooLargeException(ProviderResponseLimits.Image);
        }

        var bytes = Convert.FromBase64String(base64);
        if (bytes.LongLength > ProviderResponseLimits.Image)
        {
            throw new ProviderResponseTooLargeException(ProviderResponseLimits.Image);
        }

        return bytes;
    }

    /// <summary>These APIs do not list input modalities, so no model is known to take images.</summary>
    public Task<IReadOnlyList<ImageGen.Core.Domain.Models.ChatModel>> ListChatModelsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ImageGen.Core.Domain.Models.ChatModel>>([]);

    public async Task<AssistantChatResult> CompleteChatAsync(AssistantChatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var httpRequest = await CreateAuthorizedRequestAsync(
            HttpMethod.Post, "chat/completions", OpenAiChatWire.BuildBody(request), cancellationToken);
        var root = await SendJsonAsync(httpRequest, cancellationToken);
        var (text, toolCalls) = OpenAiChatWire.ReadReply(root, Config.DisplayName);
        return new AssistantChatResult(text, toolCalls, null, root["usage"]?["prompt_tokens"] is JsonValue tokens && tokens.TryGetValue<int>(out var count) ? count : null);
    }
}

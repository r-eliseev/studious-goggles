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

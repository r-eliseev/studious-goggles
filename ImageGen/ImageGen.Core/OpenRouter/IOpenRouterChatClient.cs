namespace ImageGen.Core.OpenRouter;

/// <summary>Chat completions for the session assistant. Throws <see cref="Providers.ProviderException"/> on provider errors.</summary>
public interface IOpenRouterChatClient
{
    Task<OpenRouterChatResult> CompleteChatAsync(OpenRouterChatRequest request, CancellationToken cancellationToken);
}

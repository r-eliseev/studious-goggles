namespace ImageGen.Core.OpenRouter;

/// <summary>
/// Chat completions through OpenRouter. The skeleton assistant works only through OpenRouter,
/// so this is not hidden behind <see cref="Providers.IModelProvider"/>.
/// </summary>
public interface IOpenRouterChatClient
{
    /// <summary>Throws <see cref="Providers.ProviderException"/> on provider errors.</summary>
    Task<OpenRouterChatResult> CompleteChatAsync(OpenRouterChatRequest request, CancellationToken cancellationToken);
}

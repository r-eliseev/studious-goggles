using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Providers;

/// <summary>Chat for the session assistant, in the neutral form of <see cref="AssistantChatRequest"/>.</summary>
public interface IAssistantChatClient : IProviderConnection
{
    /// <summary>Throws <see cref="ProviderException"/> on provider errors.</summary>
    Task<AssistantChatResult> CompleteChatAsync(AssistantChatRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Chat models of the provider. A provider that does not list input modalities returns an empty list, so no model is
    /// known to take images.
    /// </summary>
    Task<IReadOnlyList<ChatModel>> ListChatModelsAsync(CancellationToken cancellationToken);
}

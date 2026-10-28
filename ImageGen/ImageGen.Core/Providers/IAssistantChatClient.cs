namespace ImageGen.Core.Providers;

/// <summary>Chat for the session assistant, in the neutral form of <see cref="AssistantChatRequest"/>.</summary>
public interface IAssistantChatClient : IProviderConnection
{
    /// <summary>Throws <see cref="ProviderException"/> on provider errors.</summary>
    Task<AssistantChatResult> CompleteChatAsync(AssistantChatRequest request, CancellationToken cancellationToken);
}

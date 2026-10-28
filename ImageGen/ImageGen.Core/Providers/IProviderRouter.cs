namespace ImageGen.Core.Providers;

/// <summary>Finds the client of a configured provider by its id. Unknown ids fail with <see cref="ProviderException"/>.</summary>
public interface IProviderRouter
{
    /// <summary>Ids of the providers that can generate images (every configured kind except Anthropic-compatible).</summary>
    IReadOnlyList<string> ImageProviderIds { get; }

    IProviderConnection GetConnection(string providerId);

    IModelProvider GetImageProvider(string providerId);

    IAssistantChatClient GetChatClient(string providerId);
}

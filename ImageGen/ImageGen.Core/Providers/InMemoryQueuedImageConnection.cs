using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Providers;

/// <summary>Combines one synchronous image/chat client with a prepared in-memory queued adapter.</summary>
public sealed class InMemoryQueuedImageConnection : IModelProvider, IAssistantChatClient
{
    private readonly IModelProvider _imageProvider;
    private readonly IAssistantChatClient _chatClient;

    public InMemoryQueuedImageConnection(
        IModelProvider imageProvider,
        IAssistantChatClient chatClient,
        string providerId,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(imageProvider);
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _imageProvider = imageProvider;
        _chatClient = chatClient;
        QueuedImages = new InMemoryQueuedImageProvider(imageProvider, providerId, timeProvider);
    }

    internal IQueuedImageProvider QueuedImages { get; }

    public Task<ApiKeyStatus> CheckKeyAsync(string apiKey, CancellationToken cancellationToken) =>
        _imageProvider.CheckKeyAsync(apiKey, cancellationToken);

    public Task<IReadOnlyList<ImageModel>> ListImageModelsAsync(CancellationToken cancellationToken) =>
        _imageProvider.ListImageModelsAsync(cancellationToken);

    public Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request, CancellationToken cancellationToken) =>
        _imageProvider.GenerateAsync(request, cancellationToken);

    public Task<AssistantChatResult> CompleteChatAsync(AssistantChatRequest request, CancellationToken cancellationToken) =>
        _chatClient.CompleteChatAsync(request, cancellationToken);

    public Task<IReadOnlyList<ChatModel>> ListChatModelsAsync(CancellationToken cancellationToken) =>
        _chatClient.ListChatModelsAsync(cancellationToken);
}

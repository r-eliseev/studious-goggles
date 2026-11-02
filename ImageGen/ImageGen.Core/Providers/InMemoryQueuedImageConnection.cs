using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Providers;

/// <summary>Combines one synchronous image/chat client with a prepared in-memory queued adapter.</summary>
public sealed class InMemoryQueuedImageConnection : IProviderConnection, IModelProvider, IAssistantChatClient, IQueuedImageProvider, ITextEmbeddingClient
{
    private readonly IModelProvider _imageProvider;
    private readonly IAssistantChatClient _chatClient;
    private readonly ITextEmbeddingClient? _textEmbeddingClient;
    private readonly InMemoryQueuedImageProvider _queuedImages;

    public InMemoryQueuedImageConnection(
        IModelProvider imageProvider,
        IAssistantChatClient chatClient,
        ISynchronousImageTransport transport,
        string providerId,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(imageProvider);
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _imageProvider = imageProvider;
        _chatClient = chatClient;
        _textEmbeddingClient = imageProvider as ITextEmbeddingClient ?? chatClient as ITextEmbeddingClient;
        _queuedImages = new InMemoryQueuedImageProvider(transport, providerId, timeProvider);
    }

    public Task<ProviderJobHandle> SubmitAsync(ImageGenerationRequest request, CancellationToken cancellationToken) =>
        _queuedImages.SubmitAsync(request, cancellationToken);

    public Task<JobUpdate> PollAsync(ProviderJobHandle handle, CancellationToken cancellationToken) =>
        _queuedImages.PollAsync(handle, cancellationToken);

    public Task<CancelOutcome> CancelAsync(ProviderJobHandle handle, CancellationToken cancellationToken) =>
        _queuedImages.CancelAsync(handle, cancellationToken);

    public Task<ApiKeyStatus> CheckKeyAsync(string apiKey, CancellationToken cancellationToken) =>
        _imageProvider.CheckKeyAsync(apiKey, cancellationToken);

    public Task<IReadOnlyList<ImageModel>> ListImageModelsAsync(CancellationToken cancellationToken) =>
        _imageProvider.ListImageModelsAsync(cancellationToken);

    public Task<AssistantChatResult> CompleteChatAsync(AssistantChatRequest request, CancellationToken cancellationToken) =>
        _chatClient.CompleteChatAsync(request, cancellationToken);

    public Task<IReadOnlyList<ChatModel>> ListChatModelsAsync(CancellationToken cancellationToken) =>
        _chatClient.ListChatModelsAsync(cancellationToken);

    public Task<EmbeddingResult> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken) =>
        (_textEmbeddingClient ?? throw new ProviderException(
            ImageGen.Core.Domain.Generations.ErrorKind.ModelUnavailable,
            "This provider does not support text embeddings."))
        .EmbedAsync(texts, cancellationToken);
}

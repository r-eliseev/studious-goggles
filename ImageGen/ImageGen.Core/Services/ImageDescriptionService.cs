using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Descriptions;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Providers;
using ImageGen.Core.Storage;

namespace ImageGen.Core.Services;

/// <summary>
/// Describes a picture with the session's assistant model (task 019). Nothing is sent when the model does not take images
/// or the file is gone. A failed or empty answer is not saved. The text is not put into the input field here: the user does that.
/// </summary>
public sealed class ImageDescriptionService(
    ISessionRepository sessions,
    IRunRepository runs,
    IProviderRouter providers,
    ChatModelCatalogService chatModels,
    IImageFileStore files,
    IImageDescriptionRepository descriptions,
    TimeProvider timeProvider)
{
    /// <exception cref="ImageDescriptionException">The model takes no images, the file is missing, or the answer is empty.</exception>
    /// <exception cref="ProviderException">The provider failed.</exception>
    public async Task<ImageDescription> DescribeAsync(
        Guid sessionId,
        Guid generationId,
        DescriptionMode mode,
        string language,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        var session = await sessions.GetAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");
        var model = session.Settings.AssistantModel;
        if (!await chatModels.IsImageInputAsync(model, cancellationToken))
        {
            throw new ImageDescriptionException(DescriptionError.ModelDoesNotAcceptImages, "The assistant model does not take images.");
        }

        var generation = await runs.GetGenerationAsync(generationId, cancellationToken)
            ?? throw new InvalidOperationException($"Generation {generationId} not found.");
        var dataUrl = await ReadImageAsync(generation, cancellationToken);

        var request = new AssistantChatRequest(
            model.ModelId,
            [
                AssistantChatMessage.System(DescriptionPrompts.For(mode, language)),
                new AssistantChatMessage(AssistantChatMessage.UserRole, DescriptionPrompts.UserText, [dataUrl]),
            ],
            []);
        var reply = await providers.GetChatClient(model.ProviderId).CompleteChatAsync(request, cancellationToken);
        var text = reply.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            throw new ImageDescriptionException(DescriptionError.EmptyResponse, "The assistant returned an empty description.");
        }

        var description = new ImageDescription
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            SourceGenerationId = generationId,
            Mode = mode,
            ProviderId = model.ProviderId,
            ModelId = model.ModelId,
            Text = text,
            CostUsd = reply.CostUsd,
            CreatedAt = timeProvider.GetUtcNow(),
        };
        await descriptions.AddAsync(description, cancellationToken);
        return description;
    }

    private async Task<string> ReadImageAsync(Generation generation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(generation.ImagePath))
        {
            throw new ImageDescriptionException(DescriptionError.SourceMissing, "The picture has no saved file.");
        }

        try
        {
            var content = await files.ReadAsync(generation.ImagePath, cancellationToken);
            return ImageDataUrl.From(content, generation.MediaType ?? "image/png");
        }
        catch (IOException)
        {
            throw new ImageDescriptionException(DescriptionError.SourceMissing, "The picture's file is missing.");
        }
    }
}

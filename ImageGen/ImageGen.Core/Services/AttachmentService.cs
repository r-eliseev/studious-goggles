using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Storage;

namespace ImageGen.Core.Services;

/// <summary>
/// Images the user attaches to a message (task 018). The file is written first and the row after it; if the row cannot be
/// written, the file is removed so that no orphan stays behind.
/// </summary>
public sealed class AttachmentService(IChatAttachmentRepository attachments, IImageFileStore files, TimeProvider timeProvider)
{
    public const int MaxAttachmentsPerMessage = 4;
    public const long MaxAttachmentBytes = 10 * 1024 * 1024;

    private static readonly string[] SupportedMediaTypes = ["image/png", "image/jpeg", "image/webp"];

    /// <exception cref="ArgumentException">Empty, too large, or not a PNG, JPEG or WebP image.</exception>
    public async Task<ChatAttachment> AddImageAsync(Guid sessionId, byte[] content, string mediaType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        if (content.Length == 0)
        {
            throw new ArgumentException("The image is empty.", nameof(content));
        }

        if (content.Length > MaxAttachmentBytes)
        {
            throw new ArgumentException("The image is larger than 10 MB.", nameof(content));
        }

        if (Array.IndexOf(SupportedMediaTypes, mediaType) < 0)
        {
            throw new ArgumentException("Only PNG, JPEG and WebP images can be attached.", nameof(mediaType));
        }

        var id = Guid.NewGuid();
        var path = await files.SaveAttachmentAsync(sessionId, id, content, mediaType, cancellationToken);
        var attachment = new ChatAttachment
        {
            Id = id,
            SessionId = sessionId,
            RelativePath = path,
            MediaType = mediaType,
            CreatedAt = timeProvider.GetUtcNow(),
        };
        try
        {
            await attachments.AddAsync(attachment, cancellationToken);
        }
        catch
        {
            await files.DeleteFileAsync(path, CancellationToken.None);
            throw;
        }

        return attachment;
    }

    /// <summary>The attachment as a data URL, for the assistant or a generation request.</summary>
    /// <exception cref="InvalidOperationException">The attachment does not exist.</exception>
    public async Task<string> ReadDataUrlAsync(Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await attachments.GetAsync(attachmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Attachment {attachmentId} not found.");
        var content = await files.ReadAsync(attachment.RelativePath, cancellationToken);
        return ImageDataUrl.From(content, attachment.MediaType);
    }

    /// <summary>Ids of the images attached to the message, in the order they were added.</summary>
    public async Task<IReadOnlyList<Guid>> ListIdsByMessageAsync(Guid messageId, CancellationToken cancellationToken) =>
        (await attachments.ListByMessageAsync(messageId, cancellationToken)).Select(attachment => attachment.Id).ToList();

    public Task LinkToMessageAsync(IReadOnlyList<Guid> attachmentIds, Guid messageId, CancellationToken cancellationToken) =>
        attachmentIds.Count == 0
            ? Task.CompletedTask
            : attachments.LinkToMessageAsync(attachmentIds, messageId, cancellationToken);
}

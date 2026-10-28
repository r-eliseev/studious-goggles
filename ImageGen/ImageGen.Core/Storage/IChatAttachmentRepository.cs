using ImageGen.Core.Domain.Chat;

namespace ImageGen.Core.Storage;

public interface IChatAttachmentRepository
{
    Task AddAsync(ChatAttachment attachment, CancellationToken cancellationToken);

    Task<ChatAttachment?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Links the attachments to the message that the user sent with them.</summary>
    Task LinkToMessageAsync(IReadOnlyList<Guid> attachmentIds, Guid messageId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatAttachment>> ListByMessageAsync(Guid messageId, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

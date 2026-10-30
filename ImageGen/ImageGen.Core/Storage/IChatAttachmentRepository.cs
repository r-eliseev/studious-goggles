using ImageGen.Core.Domain.Chat;

namespace ImageGen.Core.Storage;

public interface IChatAttachmentRepository
{
    Task AddAsync(ChatAttachment attachment, CancellationToken cancellationToken);

    Task<ChatAttachment?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatAttachment>> ListByMessageAsync(Guid messageId, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

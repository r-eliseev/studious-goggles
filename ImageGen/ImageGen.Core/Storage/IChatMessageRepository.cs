using ImageGen.Core.Domain.Chat;

namespace ImageGen.Core.Storage;

public interface IChatMessageRepository
{
    Task AddAsync(ChatMessage message, CancellationToken cancellationToken);

    /// <summary>Atomically inserts a user message and links all of its attachments.</summary>
    Task AddUserWithAttachmentsAsync(ChatMessage message, IReadOnlyList<Guid> attachmentIds, CancellationToken cancellationToken);

    /// <summary>Messages of the session ordered by <see cref="ChatMessage.CreatedAt"/> ascending.</summary>
    Task<IReadOnlyList<ChatMessage>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken);
}

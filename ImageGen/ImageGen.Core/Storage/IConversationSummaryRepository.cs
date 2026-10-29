using ImageGen.Core.Domain.Chat;

namespace ImageGen.Core.Storage;

public interface IConversationSummaryRepository
{
    /// <summary>The session's summary, or null when it has none yet.</summary>
    Task<ConversationSummary?> GetAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Saves the summary, replacing the session's previous one.</summary>
    Task UpsertAsync(ConversationSummary summary, CancellationToken cancellationToken);
}

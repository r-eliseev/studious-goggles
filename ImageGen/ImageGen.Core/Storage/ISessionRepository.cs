using ImageGen.Core.Domain;

namespace ImageGen.Core.Storage;

public interface ISessionRepository
{
    Task AddAsync(Session session, CancellationToken cancellationToken);

    Task UpdateAsync(Session session, CancellationToken cancellationToken);

    Task<Session?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>All sessions ordered by <see cref="Session.UpdatedAt"/> descending.</summary>
    Task<IReadOnlyList<Session>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Deletes the session with its messages, runs and generations.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

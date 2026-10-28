using ImageGen.Core.Domain.Descriptions;

namespace ImageGen.Core.Storage;

public interface IImageDescriptionRepository
{
    Task AddAsync(ImageDescription description, CancellationToken cancellationToken);

    /// <summary>Descriptions of the session, oldest first.</summary>
    Task<IReadOnlyList<ImageDescription>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken);
}

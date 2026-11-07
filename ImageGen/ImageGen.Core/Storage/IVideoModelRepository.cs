using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Storage;

public interface IVideoModelRepository
{
    Task ReplaceAsync(string providerId, IReadOnlyList<VideoModel> models, CancellationToken cancellationToken);

    Task<IReadOnlyList<VideoModel>> ListAsync(string providerId, CancellationToken cancellationToken);
}

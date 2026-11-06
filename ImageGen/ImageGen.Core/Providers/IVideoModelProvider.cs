using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Providers;

public interface IVideoModelProvider
{
    Task<IReadOnlyList<VideoModel>> ListVideoModelsAsync(CancellationToken cancellationToken);
}

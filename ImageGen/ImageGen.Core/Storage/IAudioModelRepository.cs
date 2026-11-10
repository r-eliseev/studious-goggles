using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Storage;

public interface IAudioModelRepository
{
    Task ReplaceAsync(string providerId, IReadOnlyList<AudioModel> models, CancellationToken cancellationToken);

    Task<IReadOnlyList<AudioModel>> ListAsync(string providerId, CancellationToken cancellationToken);
}

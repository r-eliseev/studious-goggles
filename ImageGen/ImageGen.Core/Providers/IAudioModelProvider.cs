using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Providers;

public interface IAudioModelProvider
{
    Task<IReadOnlyList<AudioModel>> ListAudioModelsAsync(CancellationToken cancellationToken);
}

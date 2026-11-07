namespace ImageGen.Presentation.Services;

/// <summary>Video poster extraction is unavailable in this shell.</summary>
public sealed class UnsupportedVideoPosterService : IVideoPosterService
{
    public bool IsSupported => false;

    public Task<PosterInfo?> GetPosterAsync(string videoPath, CancellationToken cancellationToken) => Task.FromResult<PosterInfo?>(null);
}

namespace ImageGen.Presentation.Services;

public sealed record PosterInfo(string Path, int Width, int Height);

/// <summary>Extracts and caches a representative frame from a local video.</summary>
public interface IVideoPosterService
{
    bool IsSupported { get; }

    Task<PosterInfo?> GetPosterAsync(string videoPath, CancellationToken cancellationToken);
}

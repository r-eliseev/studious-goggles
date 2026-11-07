namespace ImageGen.Presentation.Services;

/// <summary>Media playback is not available in this shell.</summary>
public sealed class UnsupportedMediaPlayerService : IMediaPlayerService
{
    public bool IsSupported => false;

    public Task ShowAsync(string filePath, string accessibleLabel, CancellationToken cancellationToken) => Task.CompletedTask;
}

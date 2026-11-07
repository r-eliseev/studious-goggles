namespace ImageGen.Presentation.Services;

/// <summary>Opens a media file in a dedicated player.</summary>
public interface IMediaPlayerService
{
    bool IsSupported { get; }

    Task ShowAsync(string filePath, string accessibleLabel, CancellationToken cancellationToken);
}

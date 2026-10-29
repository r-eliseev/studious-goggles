namespace ImageGen.Presentation.Services;

/// <summary>
/// Saves a finished picture where the platform keeps pictures: a save dialog on desktop, the gallery or Photos on phones.
/// Each shell implements it (task 080).
/// </summary>
public interface IImageSaver
{
    /// <summary>Caption of the save action in this shell, for example «Save image as…» or «Save to Photos».</summary>
    string ActionLabel { get; }

    Task<ImageSaveResult> SaveAsync(string sourcePath, string suggestedFileName, string mediaType, CancellationToken cancellationToken);
}

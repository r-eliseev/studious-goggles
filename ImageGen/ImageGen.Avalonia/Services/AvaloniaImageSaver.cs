using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Avalonia.Services;

/// <summary>Saves a picture through the system «save as» dialog of the main window (task 080).</summary>
public sealed class AvaloniaImageSaver(Func<Window?> owner) : IImageSaver
{
    public string ActionLabel => AppResources.SaveImageAs;

    public async Task<ImageSaveResult> SaveAsync(
        string sourcePath, string suggestedFileName, string mediaType, CancellationToken cancellationToken)
    {
        var window = owner();
        if (window is null)
        {
            return ImageSaveResult.Failed;
        }

        var extension = ImageFileNames.Extension(mediaType);
        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedFileName,
            DefaultExtension = extension.TrimStart('.'),
            FileTypeChoices = [new FilePickerFileType(mediaType) { Patterns = ["*" + extension] }],
        });
        if (file is null)
        {
            return ImageSaveResult.Cancelled;
        }

        try
        {
            await using var source = File.OpenRead(sourcePath);
            await using var target = await file.OpenWriteAsync();
            await CopyAsync(source, target, cancellationToken);
            return ImageSaveResult.Saved;
        }
        catch (UnauthorizedAccessException)
        {
            return ImageSaveResult.PermissionDenied;
        }
        catch (IOException)
        {
            return ImageSaveResult.Failed;
        }
    }

    /// <summary>Copies the picture bytes into the chosen file.</summary>
    public static Task CopyAsync(Stream source, Stream target, CancellationToken cancellationToken) =>
        source.CopyToAsync(target, cancellationToken);
}

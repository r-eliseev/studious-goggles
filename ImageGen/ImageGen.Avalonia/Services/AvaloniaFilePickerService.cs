using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Avalonia.Services;

/// <summary>Picks one image with the system file dialog of the main window (Avalonia storage provider).</summary>
public sealed class AvaloniaFilePickerService(Func<Window?> owner) : IFilePickerService
{
    private static readonly FilePickerFileType ImageFiles = new("Images")
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp"],
    };

    public async Task<PickedImage?> PickImageAsync(CancellationToken cancellationToken)
    {
        var window = owner();
        if (window is null)
        {
            return null;
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = AppResources.AttachImageButton,
            AllowMultiple = false,
            FileTypeFilter = [ImageFiles],
        });
        var file = files.FirstOrDefault();
        if (file is null)
        {
            return null;
        }

        await using var stream = await file.OpenReadAsync();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        return new PickedImage(memory.ToArray(), ImageMediaType.FromFileName(file.Name), file.Name);
    }
}

using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using Microsoft.Maui.Storage;

namespace ImageGen.Maui.Services;

/// <summary>Picks one image with the platform picker (MAUI Essentials).</summary>
public sealed class MauiFilePickerService : IFilePickerService
{
    public async Task<PickedImage?> PickImageAsync(CancellationToken cancellationToken)
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = AppResources.AttachImageButton,
            FileTypes = FilePickerFileType.Images,
        });
        if (file is null)
        {
            return null;
        }

        await using var stream = await file.OpenReadAsync();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        return new PickedImage(memory.ToArray(), ImageMediaType.FromFileName(file.FileName), file.FileName);
    }
}

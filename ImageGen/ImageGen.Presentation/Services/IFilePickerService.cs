namespace ImageGen.Presentation.Services;

/// <summary>Lets the user pick one image file. Each shell implements it with its own file dialog.</summary>
public interface IFilePickerService
{
    /// <summary>The picked image, or null when the user cancelled.</summary>
    Task<PickedImage?> PickImageAsync(CancellationToken cancellationToken);
}

/// <param name="Content">The file's bytes.</param>
/// <param name="MediaType">image/png, image/jpeg or image/webp, from the file type.</param>
/// <param name="FileName">Name shown to the user.</param>
public sealed record PickedImage(byte[] Content, string MediaType, string FileName);

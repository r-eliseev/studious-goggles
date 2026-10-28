namespace ImageGen.Presentation.Services;

/// <summary>Media type of a picked image from its file name. Shells use it for every file dialog.</summary>
public static class ImageMediaType
{
    /// <summary>image/png, image/jpeg or image/webp by extension; anything else is application/octet-stream and is refused later.</summary>
    public static string FromFileName(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        _ => "application/octet-stream",
    };
}

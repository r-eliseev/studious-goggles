namespace ImageGen.Presentation.Services;

/// <summary>The name a saved picture gets by default: <c>imagegen-</c> with the first 8 characters of the generation id.</summary>
public static class ImageFileNames
{
    public static string Suggested(Guid generationId, string mediaType) =>
        $"imagegen-{generationId.ToString("N")[..8]}{Extension(mediaType)}";

    /// <summary>The extension for a media type; an unknown type gets .png, the format the provider returns by default.</summary>
    public static string Extension(string mediaType) => mediaType switch
    {
        "image/jpeg" => ".jpg",
        "image/webp" => ".webp",
        "video/mp4" => ".mp4",
        "video/webm" => ".webm",
        "video/quicktime" => ".mov",
        _ => ".png",
    };
}

namespace ImageGen.Core.Services;

/// <summary>Builds the data URL a chat request carries an image in: <c>data:&lt;media type&gt;;base64,&lt;data&gt;</c>.</summary>
public static class ImageDataUrl
{
    public static string From(byte[] content, string mediaType)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        return $"data:{mediaType};base64,{Convert.ToBase64String(content)}";
    }
}

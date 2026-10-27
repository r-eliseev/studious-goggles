using ImageGen.Core.Platform;
using ImageGen.Core.Storage;

namespace ImageGen.Data;

/// <summary>Stores images as files under <c>{DataDirectory}/images/{sessionId}/{generationId}.{ext}</c>.</summary>
public sealed class FileImageStore(IAppPaths appPaths) : IImageFileStore
{
    public const string ImagesFolder = "images";

    public async Task<string> SaveAsync(
        Guid sessionId,
        Guid generationId,
        byte[] content,
        string mediaType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);

        var relativePath = Path.Combine(
            ImagesFolder,
            sessionId.ToString("N"),
            generationId.ToString("N") + GetExtension(mediaType));
        var fullPath = GetFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);
        return relativePath;
    }

    public string GetFullPath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        return Path.Combine(appPaths.DataDirectory, relativePath);
    }

    public Task DeleteSessionFilesAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(appPaths.DataDirectory, ImagesFolder, sessionId.ToString("N"));
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }

        return Task.CompletedTask;
    }

    /// <summary>Maps a media type to a file extension with a leading dot.</summary>
    public static string GetExtension(string mediaType) => mediaType.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/jpg" => ".jpg",
        "image/webp" => ".webp",
        "image/gif" => ".gif",
        _ => ".bin",
    };
}

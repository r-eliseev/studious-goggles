using ImageGen.Core.Platform;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging;

namespace ImageGen.Data;

/// <summary>Stores images as files under <c>{DataDirectory}/images/{sessionId}/{generationId}.{ext}</c>.</summary>
public sealed class FileImageStore(IAppPaths appPaths, ILogger<FileImageStore> logger) : IImageFileStore
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

        // The relative path is stored in the database, so it uses "/" on every platform.
        var relativePath = $"{ImagesFolder}/{sessionId:N}/{generationId:N}{GetExtension(mediaType)}";
        var fullPath = GetFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);
        logger.LogDebug("Image saved: {Path} ({Bytes} bytes, {MediaType}).", relativePath, content.Length, mediaType);
        return relativePath;
    }

    /// <summary>Writes an image the user attached, under <c>images/{sessionId}/attachments/{attachmentId}.{ext}</c>.</summary>
    public async Task<string> SaveAttachmentAsync(
        Guid sessionId,
        Guid attachmentId,
        byte[] content,
        string mediaType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        var relativePath = $"{ImagesFolder}/{sessionId:N}/attachments/{attachmentId:N}{GetExtension(mediaType)}";
        var fullPath = GetFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);
        return relativePath;
    }

    public Task DeleteFileAsync(string relativePath, CancellationToken cancellationToken)
    {
        var fullPath = GetFullPath(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public Task<byte[]> ReadAsync(string relativePath, CancellationToken cancellationToken)
    {
        var fullPath = GetFullPath(relativePath);
        // A missing folder gives DirectoryNotFoundException from the framework; the contract is FileNotFoundException.
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The image file does not exist.", fullPath);
        }

        return File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    public string GetFullPath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        // Stored paths use "/"; image loaders get the separators of the system (WinRT does not open "/" paths).
        var systemRelative = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(appPaths.DataDirectory, systemRelative);
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

    /// <summary>Maps a media type to a file extension with a leading dot. Unknown types get <c>.bin</c>.</summary>
    internal static string GetExtension(string mediaType) => mediaType.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/jpg" => ".jpg",
        "image/webp" => ".webp",
        "image/gif" => ".gif",
        _ => ".bin",
    };
}

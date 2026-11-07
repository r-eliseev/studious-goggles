using System.Text.RegularExpressions;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Storage;
using MetadataExtractor;
using MetadataExtractor.Formats.Gif;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Png;
using MetadataExtractor.Formats.Riff;
using MetadataExtractor.Formats.WebP;
using Microsoft.Extensions.Logging;
using Directory = System.IO.Directory;
using ImageMetadataReader = MetadataExtractor.ImageMetadataReader;

namespace ImageGen.Data;

/// <summary>Stores images as files under <c>{DataDirectory}/images/{sessionId}/{generationId}.{ext}</c>.</summary>
public sealed class FileImageStore : IImageFileStore
{
    private readonly IAppPaths appPaths;
    private readonly ILogger<FileImageStore> logger;
    private readonly Func<string, ImageDimensions?> readDimensions;
    private readonly Action<string> deleteOrphanedTemporaryFile;
    private static readonly Regex OrphanedImageTemporaryFilePattern = new(@"^\..+\.[0-9a-f]{32}\.tmp$", RegexOptions.CultureInvariant);
    private static readonly Regex OrphanedDefaultsTemporaryFilePattern = new(@"^defaults\.json\.[0-9a-f]{32}\.tmp$", RegexOptions.CultureInvariant);
    public const string ImagesFolder = "images";
    private readonly AtomicImageFileWriter atomicWriter;
    private readonly SemaphoreSlim _metadataReadGate = new(2, 2);
    private readonly object dimensionReadLock = new();
    private TaskCompletionSource dimensionReadsIdle = CompletedSource();
    private int activeDimensionReads;

    public FileImageStore(IAppPaths appPaths, ILogger<FileImageStore> logger)
        : this(appPaths, logger, ReadDimensions)
    {
    }

    internal FileImageStore(
        IAppPaths appPaths,
        ILogger<FileImageStore> logger,
        Func<string, ImageDimensions?> readDimensions,
        Action<string>? deleteOrphanedTemporaryFile = null)
    {
        this.appPaths = appPaths;
        this.logger = logger;
        this.readDimensions = readDimensions;
        this.deleteOrphanedTemporaryFile = deleteOrphanedTemporaryFile ?? File.Delete;
        atomicWriter = new AtomicImageFileWriter(logger: logger);
    }

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
        await atomicWriter.WriteAsync(fullPath, content, cancellationToken);
        LogSavedSafely(logger, relativePath, content.Length, mediaType);
        return relativePath;
    }

    public async Task<string> SaveMediaStreamAsync(
        Guid sessionId,
        Guid generationId,
        Stream content,
        string mediaType,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);

        var extension = GetMediaExtension(mediaType);
        var relativePath = $"{ImagesFolder}/{sessionId:N}/{generationId:N}{extension}";
        var fullPath = GetFullPath(relativePath);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        var ownsTemporaryFile = false;
        var committed = false;
        long byteCount = 0;
        try
        {
            await using (var destination = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                ownsTemporaryFile = true;
                var buffer = new byte[81920];
                while (true)
                {
                    var read = await content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    if (read > maxBytes - byteCount)
                    {
                        throw new ProviderException(
                            ErrorKind.Unknown,
                            "Media response exceeded the configured size limit.",
                            innerException: new ProviderResponseTooLargeException(maxBytes, byteCount + read));
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    byteCount += read;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, fullPath, overwrite: true);
            committed = true;
            try
            {
                logger.LogDebug("Media saved: {Path} ({Bytes} bytes, {MediaType}).", relativePath, byteCount, mediaType);
            }
            catch
            {
                // Diagnostics cannot change the result after the file has been committed.
            }

            return relativePath;
        }
        finally
        {
            if (ownsTemporaryFile && !committed)
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(
                        exception,
                        "Could not remove temporary media file {FileName} ({ExceptionType}).",
                        Path.GetFileName(temporaryPath),
                        exception.GetType().Name);
                }
            }
        }
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
        await atomicWriter.WriteAsync(fullPath, content, cancellationToken);
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

    public async Task<ImageDimensions?> TryReadDimensionsAsync(string relativePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        BeginDimensionRead();
        try
        {
            await _metadataReadGate.WaitAsync(cancellationToken);
            try
            {
                var fullPath = GetFullPath(relativePath);
                return await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        return readDimensions(fullPath);
                    }
                    catch (Exception exception) when (exception is IOException
                        or UnauthorizedAccessException
                        or MetadataException
                        or ImageProcessingException
                        or RiffProcessingException)
                    {
                        logger.LogDebug(exception, "Could not read image dimensions from {Path}.", relativePath);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    return (ImageDimensions?)null;
                }, CancellationToken.None);
            }
            finally
            {
                _metadataReadGate.Release();
            }
        }
        finally
        {
            EndDimensionRead();
        }
    }

    public Task WhenIdleAsync()
    {
        lock (dimensionReadLock)
        {
            return activeDimensionReads == 0 ? Task.CompletedTask : dimensionReadsIdle.Task;
        }
    }

    /// <summary>Deletes old temporary files left by interrupted atomic writes.</summary>
    public int DeleteOrphanedTemporaryFiles(TimeSpan olderThan, DateTimeOffset now)
    {
        var deletedCount = 0;
        var imagesDirectory = Path.Combine(appPaths.DataDirectory, ImagesFolder);
        if (Directory.Exists(imagesDirectory))
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(imagesDirectory, "*", SearchOption.AllDirectories))
                {
                    if (OrphanedImageTemporaryFilePattern.IsMatch(Path.GetFileName(path)))
                    {
                        TryDeleteOldTemporaryFile(path, olderThan, now, ref deletedCount);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                LogTemporaryFileCleanupFailure(imagesDirectory, exception);
            }
        }

        try
        {
            foreach (var path in Directory.EnumerateFiles(appPaths.DataDirectory, "defaults.json.*.tmp", SearchOption.TopDirectoryOnly))
            {
                if (OrphanedDefaultsTemporaryFilePattern.IsMatch(Path.GetFileName(path)))
                {
                    TryDeleteOldTemporaryFile(path, olderThan, now, ref deletedCount);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogTemporaryFileCleanupFailure(appPaths.DataDirectory, exception);
        }

        return deletedCount;
    }

    private void TryDeleteOldTemporaryFile(string path, TimeSpan olderThan, DateTimeOffset now, ref int deletedCount)
    {
        try
        {
            if (now - File.GetLastWriteTimeUtc(path) <= olderThan)
            {
                return;
            }

            deleteOrphanedTemporaryFile(path);
            deletedCount++;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogTemporaryFileCleanupFailure(Path.GetRelativePath(appPaths.DataDirectory, path), exception);
        }
    }

    private void LogTemporaryFileCleanupFailure(string relativePath, Exception exception) =>
        logger.LogWarning(exception, "Could not remove orphaned temporary file {Path} ({ExceptionType}).", relativePath, exception.GetType().Name);

    private void BeginDimensionRead()
    {
        lock (dimensionReadLock)
        {
            if (activeDimensionReads++ == 0)
            {
                dimensionReadsIdle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    private void EndDimensionRead()
    {
        TaskCompletionSource? completed = null;
        lock (dimensionReadLock)
        {
            if (--activeDimensionReads == 0)
            {
                completed = dimensionReadsIdle;
            }
        }

        completed?.TrySetResult();
    }

    private static TaskCompletionSource CompletedSource()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }

    private static ImageDimensions? ReadDimensions(string fullPath)
    {
        var directories = ImageMetadataReader.ReadMetadata(fullPath);
        var jpeg = directories.OfType<JpegDirectory>().FirstOrDefault();
        if (jpeg is not null && TryGetDimensions(jpeg, JpegDirectory.TagImageWidth, JpegDirectory.TagImageHeight, out var dimensions)) return dimensions;
        var png = directories.OfType<PngDirectory>().FirstOrDefault();
        if (png is not null && TryGetDimensions(png, PngDirectory.TagImageWidth, PngDirectory.TagImageHeight, out dimensions)) return dimensions;
        var webp = directories.OfType<WebPDirectory>().FirstOrDefault();
        if (webp is not null && TryGetDimensions(webp, WebPDirectory.TagImageWidth, WebPDirectory.TagImageHeight, out dimensions)) return dimensions;
        var gif = directories.OfType<GifHeaderDirectory>().FirstOrDefault();
        return gif is not null && TryGetDimensions(gif, GifHeaderDirectory.TagImageWidth, GifHeaderDirectory.TagImageHeight, out dimensions) ? dimensions : null;
    }

    private static bool TryGetDimensions(MetadataExtractor.Directory directory, int widthTag, int heightTag, out ImageDimensions dimensions)
    {
        dimensions = default;
        if (!directory.ContainsTag(widthTag) || !directory.ContainsTag(heightTag))
        {
            return false;
        }

        var width = directory.GetInt32(widthTag);
        var height = directory.GetInt32(heightTag);
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        dimensions = new ImageDimensions(width, height);
        return true;
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

    public Task<int> DeleteAllMediaAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var images = Path.Combine(appPaths.DataDirectory, ImagesFolder);
        if (!Directory.Exists(images))
        {
            return Task.FromResult(0);
        }

        var fileCount = Directory.GetFiles(images, "*", SearchOption.AllDirectories).Length;
        Directory.Delete(images, recursive: true);
        return Task.FromResult(fileCount);
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

    private static string GetMediaExtension(string mediaType) => mediaType.ToLowerInvariant() switch
    {
        "video/mp4" => ".mp4",
        "video/webm" => ".webm",
        "audio/mpeg" => ".mp3",
        "audio/wav" => ".wav",
        "audio/ogg" => ".ogg",
        "audio/flac" => ".flac",
        _ => throw new ArgumentException("Unsupported media type.", nameof(mediaType)),
    };

    private static void LogSavedSafely(ILogger logger, string relativePath, int byteCount, string mediaType)
    {
        try
        {
            logger.LogDebug("Image saved: {Path} ({Bytes} bytes, {MediaType}).", relativePath, byteCount, mediaType);
        }
        catch
        {
            // Diagnostics cannot change the result after the file has been committed.
        }
    }
}

namespace ImageGen.Core.Storage;

public interface IImageFileStore
{
    /// <summary>Writes the image and returns its path relative to the data directory.</summary>
    Task<string> SaveAsync(Guid sessionId, Guid generationId, byte[] content, string mediaType, CancellationToken cancellationToken);

    /// <summary>Writes a file the user attached; it goes to the session's attachment folder, not to a generation.</summary>
    Task<string> SaveAttachmentAsync(Guid sessionId, Guid attachmentId, byte[] content, string mediaType, CancellationToken cancellationToken);

    /// <summary>Deletes one file by its relative path. Does nothing if it does not exist.</summary>
    Task DeleteFileAsync(string relativePath, CancellationToken cancellationToken);

    /// <summary>Reads the image at a relative path. A missing file throws <see cref="FileNotFoundException"/>.</summary>
    Task<byte[]> ReadAsync(string relativePath, CancellationToken cancellationToken);

    /// <summary>Reads raster pixel dimensions from a stored image, or returns null for missing or unreadable data.</summary>
    Task<ImageDimensions?> TryReadDimensionsAsync(string relativePath, CancellationToken cancellationToken);

    /// <summary>Converts a relative path returned by <see cref="SaveAsync"/> to an absolute path.</summary>
    string GetFullPath(string relativePath);

    /// <summary>Deletes all image files of the session. Does nothing if there are none.</summary>
    Task DeleteSessionFilesAsync(Guid sessionId, CancellationToken cancellationToken);
}

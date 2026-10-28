namespace ImageGen.Core.Storage;

public interface IImageFileStore
{
    /// <summary>Writes the image and returns its path relative to the data directory.</summary>
    Task<string> SaveAsync(Guid sessionId, Guid generationId, byte[] content, string mediaType, CancellationToken cancellationToken);

    /// <summary>Reads the image at a relative path. A missing file throws <see cref="FileNotFoundException"/>.</summary>
    Task<byte[]> ReadAsync(string relativePath, CancellationToken cancellationToken);

    /// <summary>Converts a relative path returned by <see cref="SaveAsync"/> to an absolute path.</summary>
    string GetFullPath(string relativePath);

    /// <summary>Deletes all image files of the session. Does nothing if there are none.</summary>
    Task DeleteSessionFilesAsync(Guid sessionId, CancellationToken cancellationToken);
}

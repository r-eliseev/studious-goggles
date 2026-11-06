using Microsoft.Extensions.Logging;

namespace ImageGen.Data;

internal sealed class AtomicImageFileWriter
{
    private readonly Func<string, string> temporaryPathFactory;
    private readonly Func<Stream, ReadOnlyMemory<byte>, CancellationToken, Task> writeAsync;
    private readonly Action<string> deleteFile;
    private readonly ILogger? logger;

    public AtomicImageFileWriter(
        Func<string, string>? temporaryPathFactory = null,
        Func<Stream, ReadOnlyMemory<byte>, CancellationToken, Task>? writeAsync = null,
        Action<string>? deleteFile = null,
        ILogger? logger = null)
    {
        this.temporaryPathFactory = temporaryPathFactory ?? ((finalPath) => Path.Combine(Path.GetDirectoryName(finalPath)!, $".{Path.GetFileName(finalPath)}.{Guid.NewGuid():N}.tmp"));
        this.writeAsync = writeAsync ?? ((stream, content, cancellationToken) => stream.WriteAsync(content, cancellationToken).AsTask());
        this.deleteFile = deleteFile ?? File.Delete;
        this.logger = logger;
    }

    public async Task WriteAsync(string finalPath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalPath);
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(finalPath) ?? throw new ArgumentException("Final path must have a directory.", nameof(finalPath));
        Directory.CreateDirectory(directory);
        var temporaryPath = temporaryPathFactory(finalPath);
        var ownsTemporaryFile = false;
        var committed = false;
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                ownsTemporaryFile = true;
                await writeAsync(stream, content, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, finalPath, overwrite: true);
            committed = true;
        }
        finally
        {
            if (ownsTemporaryFile && !committed)
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        deleteFile(temporaryPath);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    logger?.LogWarning(
                        exception,
                        "Could not remove temporary image file {FileName} ({ExceptionType}).",
                        Path.GetFileName(temporaryPath),
                        exception.GetType().Name);
                }
            }
        }
    }
}

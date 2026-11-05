namespace ImageGen.Presentation.Logging;

/// <summary>Lists, copies, and clears the application's log files.</summary>
public static class LogFiles
{
    private const string Pattern = "imagegen-*.log";

    public static IReadOnlyList<string> List(string logDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        if (!Directory.Exists(logDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(logDirectory, Pattern, SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();
    }

    public static int DeleteAllExceptNewest(string logDirectory)
    {
        var files = List(logDirectory);
        var deleted = 0;
        foreach (var file in files.Take(Math.Max(0, files.Count - 1)))
        {
            try
            {
                File.Delete(file);
                deleted++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Keep going so one locked file does not prevent clearing the others.
            }
        }

        return deleted;
    }

    public static async Task<IReadOnlyList<string>> CopyForSharingAsync(
        string logDirectory,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, recursive: true);
        }

        Directory.CreateDirectory(targetDirectory);
        var copies = new List<string>();
        foreach (var source in List(logDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = Path.Combine(targetDirectory, Path.GetFileName(source));
            await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, useAsync: true);
            await using var targetStream = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await sourceStream.CopyToAsync(targetStream, cancellationToken).ConfigureAwait(false);
            copies.Add(target);
        }

        return copies;
    }
}

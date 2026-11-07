using ImageGen.Core.Search;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Services;

public sealed class HistoryService(
    ISessionRepository sessions,
    IImageFileStore files,
    ISessionGenerationCoordinator generationCoordinator,
    SessionChangeNotifier changes,
    ILogger<HistoryService> logger,
    ISemanticIndexStatusProvider? semanticIndex = null)
{
    public async Task<int> ClearAllAsync(CancellationToken cancellationToken)
    {
        var existing = await sessions.ListAsync(cancellationToken);
        foreach (var session in existing)
        {
            await generationCoordinator.StopSessionAsync(session.Id, CancellationToken.None);
        }

        var deletedSessions = await sessions.DeleteAllAsync(CancellationToken.None);
        var deletedFiles = 0;
        try
        {
            deletedFiles = await files.DeleteAllMediaAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                "Could not delete all session media ({ExceptionType}).",
                exception.GetType().Name);
            logger.LogDebug(exception, "Could not delete all session media.");
        }

        changes.NotifyAll();
        if (semanticIndex?.GetStatusSnapshot().Enabled == true)
        {
            await semanticIndex.GetStatusAsync(CancellationToken.None);
        }

        logger.LogInformation("Deleted {SessionCount} sessions and {FileCount} media files.", deletedSessions, deletedFiles);
        return deletedSessions;
    }
}

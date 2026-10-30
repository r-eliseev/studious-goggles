namespace ImageGen.Core.Services;

/// <summary>Coordinates generation job shutdown with deletion of a session.</summary>
public interface ISessionGenerationCoordinator
{
    Task StopSessionAsync(Guid sessionId, CancellationToken cancellationToken);
}

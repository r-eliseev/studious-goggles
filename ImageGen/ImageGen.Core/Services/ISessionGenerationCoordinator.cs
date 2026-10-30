namespace ImageGen.Core.Services;

/// <summary>Stops local generation writes before a session is deleted and requests best-effort cancellation of known remote jobs.</summary>
public interface ISessionGenerationCoordinator
{
    Task StopSessionAsync(Guid sessionId, CancellationToken cancellationToken);
}

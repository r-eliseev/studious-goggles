using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Services;

/// <summary>Publishes changes to sessions so open lists can refresh one row.</summary>
public sealed class SessionChangeNotifier(ILogger<SessionChangeNotifier> logger)
{
    public event EventHandler<Guid>? SessionChanged;

    /// <summary>
    /// Notifies every subscriber. A subscriber that throws does not stop the others and does not reach the caller:
    /// the session or run is already written, and the caller must not fail because of a listener.
    /// </summary>
    public void Notify(Guid sessionId)
    {
        // The snapshot is taken once: subscribing or unsubscribing inside a handler affects the next call only.
        var handlers = SessionChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Delegate>())
        {
            try
            {
                ((EventHandler<Guid>)handler)(this, sessionId);
            }
            catch (Exception ex)
            {
                // The operational entry carries the session id only; the details stay at Debug.
                logger.LogWarning("A subscriber of session changes failed for session {SessionId}.", sessionId);
                logger.LogDebug(ex, "Details of the failure of a subscriber of session changes for session {SessionId}.", sessionId);
            }
        }
    }
}

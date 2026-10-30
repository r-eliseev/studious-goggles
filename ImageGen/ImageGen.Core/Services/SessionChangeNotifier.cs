namespace ImageGen.Core.Services;

/// <summary>Publishes changes to sessions so open lists can refresh one row.</summary>
public sealed class SessionChangeNotifier
{
    public event EventHandler<Guid>? SessionChanged;

    public void Notify(Guid sessionId) => SessionChanged?.Invoke(this, sessionId);
}

namespace ImageGen.Maui.Services;

/// <summary>Owns one fullscreen lease and follows the viewer window across pause and handler recreation.</summary>
internal sealed class MauiViewerFullscreenSession : IDisposable
{
    private readonly Microsoft.Maui.Controls.Window _window;
    private readonly IMauiFullScreenController _controller;
    private readonly EventHandler _stopped;
    private readonly EventHandler _resumed;
    private readonly EventHandler _destroying;
    private readonly EventHandler<Microsoft.Maui.Controls.HandlerChangingEventArgs> _handlerChanging;
    private readonly EventHandler _handlerChanged;
    private IDisposable? _activeLease;
    private bool _disposed;

    public MauiViewerFullscreenSession(Microsoft.Maui.Controls.Window window, IMauiFullScreenController controller)
    {
        _window = window;
        _controller = controller;
        _stopped = (_, _) => ReleaseLease();
        _resumed = (_, _) => EnterIfNeeded();
        _destroying = (_, _) => Dispose();
        _handlerChanging = (_, _) => ReleaseLease();
        _handlerChanged = (_, _) => EnterIfNeeded();
        _window.Stopped += _stopped;
        _window.Resumed += _resumed;
        _window.Destroying += _destroying;
        _window.HandlerChanging += _handlerChanging;
        _window.HandlerChanged += _handlerChanged;
        try
        {
            EnterIfNeeded();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ReleaseLease();
        _window.Stopped -= _stopped;
        _window.Resumed -= _resumed;
        _window.Destroying -= _destroying;
        _window.HandlerChanging -= _handlerChanging;
        _window.HandlerChanged -= _handlerChanged;
    }

    private void EnterIfNeeded()
    {
        if (_disposed || _activeLease is not null) return;
        _activeLease = _controller.Enter(_window);
    }

    private void ReleaseLease()
    {
        var lease = Interlocked.Exchange(ref _activeLease, null);
        lease?.Dispose();
    }
}

using Avalonia.Controls;

namespace ImageGen.Avalonia.Services;

/// <summary>Temporarily puts only the image viewer window into operating-system fullscreen.</summary>
public sealed class AvaloniaFullScreenController
{
    public IDisposable Enter(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var originalState = window.WindowState;
        var originalDecorations = window.WindowDecorations;
        var isClosed = false;
        EventHandler closed = (_, _) => isClosed = true;
        window.Closed += closed;
        try
        {
            window.WindowState = WindowState.FullScreen;
            return new Lease(() =>
            {
                window.Closed -= closed;
                if (!isClosed)
                {
                    window.WindowState = originalState;
                    window.WindowDecorations = originalDecorations;
                }
            });
        }
        catch
        {
            window.Closed -= closed;
            if (!isClosed)
            {
                window.WindowState = originalState;
                window.WindowDecorations = originalDecorations;
            }
            throw;
        }
    }

    private sealed class Lease(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}

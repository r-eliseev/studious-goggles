using System.Runtime.InteropServices;
using ImageGen.Maui.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;
using NativeWindow = Microsoft.UI.Xaml.Window;

namespace ImageGen.Maui.Platforms.Windows;

/// <summary>
/// Puts the WinUI window that hosts the viewer into fullscreen and restores its presenter, position and size on release.
/// Works only with the window the viewer was opened in, never with the first window of the application.
/// </summary>
public sealed class WindowsFullScreenController : IMauiFullScreenController
{
    public IDisposable Enter(Microsoft.Maui.Controls.Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (window.Handler?.PlatformView is not NativeWindow nativeWindow)
        {
            return EmptyLease.Instance;
        }

        var handle = WindowNative.GetWindowHandle(nativeWindow);
        var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(handle));
        return new Lease(appWindow);
    }

    private sealed class Lease : IDisposable
    {
        private readonly AppWindow _window;
        private readonly AppWindowPresenterKind _previousKind;
        private readonly RectInt32 _previousBounds;
        private readonly OverlappedPresenterState? _previousState;
        private bool _disposed;

        public Lease(AppWindow window)
        {
            _window = window;
            _previousKind = window.Presenter.Kind;
            _previousBounds = new RectInt32(window.Position.X, window.Position.Y, window.Size.Width, window.Size.Height);
            _previousState = (window.Presenter as OverlappedPresenter)?.State;
            _window.SetPresenter(AppWindowPresenterKind.FullScreen);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                _window.SetPresenter(_previousKind);
                if (_window.Presenter is OverlappedPresenter overlapped)
                {
                    RestoreOverlapped(overlapped);
                }
            }
            catch (COMException)
            {
                // The native window is already destroyed: there is nothing left to restore.
            }
        }

        private void RestoreOverlapped(OverlappedPresenter overlapped)
        {
            switch (_previousState)
            {
                case OverlappedPresenterState.Maximized:
                    overlapped.Maximize();
                    break;
                case OverlappedPresenterState.Minimized:
                    overlapped.Minimize();
                    break;
                default:
                    overlapped.Restore();
                    _window.MoveAndResize(_previousBounds);
                    break;
            }
        }
    }

    private sealed class EmptyLease : IDisposable
    {
        public static EmptyLease Instance { get; } = new();

        public void Dispose() { }
    }
}

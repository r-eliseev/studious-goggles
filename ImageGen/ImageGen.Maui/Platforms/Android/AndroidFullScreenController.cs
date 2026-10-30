using AndroidX.Core.View;
using Microsoft.Maui.Handlers;
using AndroidActivity = global::Android.App.Activity;
using AndroidView = global::Android.Views.View;
using NativeWindow = global::Android.Views.Window;

namespace ImageGen.Maui.Platforms.Android;

/// <summary>Hides Android system bars for the Activity hosting the viewer and restores their prior state.</summary>
public sealed class AndroidFullScreenController : Services.IMauiFullScreenController
{
    public IDisposable Enter(Microsoft.Maui.Controls.Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (!MainThread.IsMainThread)
        {
            throw new InvalidOperationException("Android fullscreen must be entered on the UI thread.");
        }

        if (window.Handler is not IWindowHandler { PlatformView: AndroidActivity activity }
            || activity.IsFinishing
            || activity.IsDestroyed
            || activity.Window is not { } nativeWindow)
        {
            return EmptyLease.Instance;
        }

        var decor = nativeWindow.DecorView;
        if (decor is null)
        {
            return EmptyLease.Instance;
        }

        var pending = new PendingInsetsLease(activity, nativeWindow, decor);
        pending.Start();
        return pending;
    }

    private sealed class PendingInsetsLease(AndroidActivity activity, NativeWindow window, AndroidView decor) : IDisposable
    {
        private readonly AndroidActivity _activity = activity;
        private readonly NativeWindow _window = window;
        private readonly AndroidView _decor = decor;
        private WindowInsetsControllerCompat? _insetsController;
        private int _systemBarsBehavior;
        private bool _statusVisible;
        private bool _navigationVisible;
        private bool _hasSnapshot;
        private bool _disposed;
        private Action? _retry;

        public void Start()
        {
            _retry = CaptureAndHideWhenReady;
            _decor.Post(_retry);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_retry is not null)
            {
                _decor.RemoveCallbacks(_retry);
                _retry = null;
            }
            if (_hasSnapshot && !_activity.IsFinishing && !_activity.IsDestroyed) Restore();
        }

        private void CaptureAndHideWhenReady()
        {
            if (_disposed || _activity.IsFinishing || _activity.IsDestroyed)
            {
                if (_retry is not null) _decor.RemoveCallbacks(_retry);
                _retry = null;
                return;
            }
            if (!_decor.IsAttachedToWindow)
            {
                _decor.PostDelayed(CaptureAndHideWhenReady, 50);
                return;
            }
            WindowInsetsCompat? insets;
            try
            {
                insets = ViewCompat.GetRootWindowInsets(_decor);
            }
            catch
            {
                StopWaiting();
                return;
            }
            if (insets is null)
            {
                _decor.PostDelayed(CaptureAndHideWhenReady, 50);
                return;
            }

            var statusBars = WindowInsetsCompat.Type.StatusBars();
            var navigationBars = WindowInsetsCompat.Type.NavigationBars();
            WindowInsetsControllerCompat? controller;
            try
            {
                controller = WindowCompat.GetInsetsController(_window, _decor);
            }
            catch
            {
                StopWaiting();
                return;
            }
            if (controller is null)
            {
                _decor.RemoveCallbacks(CaptureAndHideWhenReady);
                _retry = null;
                return;
            }
            _insetsController = controller;
            _statusVisible = insets.IsVisible(statusBars);
            _navigationVisible = insets.IsVisible(navigationBars);
            _systemBarsBehavior = controller.SystemBarsBehavior;
            _hasSnapshot = true;
            try
            {
                controller.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
                controller.Hide(statusBars | navigationBars);
            }
            catch
            {
                Restore();
                StopWaiting();
            }
            finally
            {
                _decor.RemoveCallbacks(CaptureAndHideWhenReady);
                _retry = null;
            }
        }

        private void StopWaiting()
        {
            _decor.RemoveCallbacks(CaptureAndHideWhenReady);
            _retry = null;
        }

        private void Restore()
        {
            var controller = _insetsController;
            if (controller is null) return;
            var statusBars = WindowInsetsCompat.Type.StatusBars();
            var navigationBars = WindowInsetsCompat.Type.NavigationBars();
            try
            {
                if (_activity.IsFinishing || _activity.IsDestroyed) return;
                try { if (_statusVisible) controller.Show(statusBars); else controller.Hide(statusBars); } catch { }
                try { if (_navigationVisible) controller.Show(navigationBars); else controller.Hide(navigationBars); } catch { }
                try { controller.SystemBarsBehavior = _systemBarsBehavior; } catch { }
            }
            finally
            {
                _insetsController = null;
                _hasSnapshot = false;
            }
        }
    }

    private sealed class EmptyLease : IDisposable
    {
        public static EmptyLease Instance { get; } = new();

        public void Dispose() { }
    }
}

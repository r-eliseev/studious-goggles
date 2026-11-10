using Avalonia.Controls;
using Avalonia.Threading;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.Logging;

namespace ImageGen.Avalonia.Services;

internal sealed class AvaloniaMediaPlayerService(
    Func<Window?> owner,
    IMediaEngine engine,
    ILogger logger) : IMediaPlayerService, IDisposable
{
    private readonly object _initializationLock = new();
    private TaskCompletionSource? _closed;
    private MediaPlayerWindow? _window;
    private IMediaPlayback? _currentPlayback;
    private bool _initializationAttempted;
    private bool _isSupported;
    private bool _disposed;

    public bool IsSupported
    {
        get
        {
            lock (_initializationLock)
            {
                if (_initializationAttempted) return _isSupported;
                _initializationAttempted = true;
                try
                {
                    _isSupported = engine.Initialize();
                    if (!_isSupported) logger.LogWarning("LibVLC initialization is unavailable.");
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "LibVLC initialization failed.");
                }

                return _isSupported;
            }
        }
    }

    public async Task ShowAsync(string filePath, string accessibleLabel, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessibleLabel);
        if (!IsSupported || _disposed || cancellationToken.IsCancellationRequested) return;
        var parent = owner();
        if (parent is null) return;

        if (_window is not null)
        {
            var previousClosed = _closed!.Task;
            _window.Close();
            await previousClosed.WaitAsync(cancellationToken);
        }

        var isVideo = IsVideo(filePath);
        var playback = engine.Create(filePath, isVideo);
        _currentPlayback = playback;
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var window = new MediaPlayerWindow(playback, accessibleLabel, isVideo, parent);
            _window = window;
            _closed = closed;
            using var registration = cancellationToken.Register(() => Dispatcher.UIThread.Post(window.Close));
            playback.Play();
            await window.ShowDialog(parent);
        }
        finally
        {
            try
            {
                DisposeCurrentPlayback();
            }
            finally
            {
                _window = null;
                closed.TrySetResult();
            }
        }
    }

    internal void CloseCurrent() => _window?.Close();

    public void Dispose()
    {
        _disposed = true;
        try
        {
            _window?.Close();
            DisposeCurrentPlayback();
        }
        finally
        {
            engine.Dispose();
        }
    }

    private void DisposeCurrentPlayback()
    {
        var playback = Interlocked.Exchange(ref _currentPlayback, null);
        if (playback is null) return;
        try
        {
            playback.Stop();
        }
        finally
        {
            playback.Dispose();
        }
    }

    private static bool IsVideo(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() is
        ".avi" or ".m4v" or ".mkv" or ".mov" or ".mp4" or ".webm";
}

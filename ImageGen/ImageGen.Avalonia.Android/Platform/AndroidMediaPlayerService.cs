using Android.Content;
using ImageGen.Presentation.Services;

namespace ImageGen.Avalonia.Platform;

/// <summary>Plays local audio and video in a dedicated AndroidX Media3 activity.</summary>
public sealed class AndroidMediaPlayerService : IMediaPlayerService
{
    private readonly SemaphoreSlim _activityLock = new(1, 1);

    public bool IsSupported => true;

    public async Task ShowAsync(string filePath, string accessibleLabel, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessibleLabel);
        cancellationToken.ThrowIfCancellationRequested();

        await _activityLock.WaitAsync(cancellationToken);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnClosed(object? sender, EventArgs args) => closed.TrySetResult();
        MediaPlayerActivity.Closed += OnClosed;
        try
        {
            var activity = MainActivity.Current
                ?? throw new InvalidOperationException("The Android activity is not available.");
            MediaPlayerActivity.PrepareLaunch();
            var intent = new Intent(activity, typeof(MediaPlayerActivity));
            intent.PutExtra(MediaPlayerActivity.FilePathExtra, Path.GetFullPath(filePath));
            intent.PutExtra(MediaPlayerActivity.AccessibleLabelExtra, accessibleLabel);
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            activity.RunOnUiThread(() =>
            {
                try
                {
                    activity.StartActivity(intent);
                    started.TrySetResult();
                }
                catch (Exception exception)
                {
                    started.TrySetException(exception);
                }
            });

            using var cancellation = cancellationToken.Register(MediaPlayerActivity.RequestClose);
            await started.Task;
            await closed.Task;
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            MediaPlayerActivity.Closed -= OnClosed;
            _activityLock.Release();
        }
    }
}

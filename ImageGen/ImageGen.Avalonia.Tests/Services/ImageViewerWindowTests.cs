using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using ImageGen.Avalonia.Services;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using NSubstitute;

namespace ImageGen.Avalonia.Tests.Services;

public sealed class ImageViewerWindowTests
{
    [AvaloniaFact]
    public async Task Escape_ClosesViewerAndDoesNotChangeOwnerState()
    {
        var owner = new Window { WindowState = WindowState.Maximized };
        owner.Show();
        var viewer = new ImageViewerWindow([new ImageViewerItem(Guid.NewGuid(), "/missing.png", "image")], 0, owner, Substitute.For<IDialogService>());
        var shown = viewer.ShowDialog(owner);
        Dispatcher.UIThread.RunJobs();

        viewer.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        await shown.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(WindowState.Maximized, owner.WindowState);
        Assert.False(viewer.IsVisible);
        viewer.DisposeImage();
        owner.Close();
    }

    [AvaloniaFact]
    public async Task MissingInitialImage_ShowsLocalizedAlertAndClosesViewer()
    {
        var dialogs = Substitute.For<IDialogService>();
        var alertShown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dialogs.AlertAsync(AppResources.ErrorTitle, AppResources.ImageViewerUnreadable).Returns(_ =>
        {
            alertShown.TrySetResult();
            return Task.CompletedTask;
        });
        var owner = new Window();
        owner.Show();
        var viewer = new ImageViewerWindow([new ImageViewerItem(Guid.NewGuid(), "/missing-image-viewer-test.png", "image")], 0, owner, dialogs);
        var shown = viewer.ShowDialog(owner);
        Dispatcher.UIThread.RunJobs();
        viewer.StartLoading();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!alertShown.Task.IsCompleted)
        {
            timeout.Token.ThrowIfCancellationRequested();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, timeout.Token);
        }
        Dispatcher.UIThread.RunJobs();
        await shown.WaitAsync(timeout.Token);

        await dialogs.Received(1).AlertAsync(AppResources.ErrorTitle, AppResources.ImageViewerUnreadable);
        viewer.DisposeImage();
        owner.Close();
    }
}

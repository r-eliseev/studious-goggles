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
    private const string MultiPixelPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAMCAIAAADQ/GvKAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAARSURBVChTY/iPA4xKoIH//wEiRB7wcYTgRwAAAABJRU5ErkJggg==";

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

    [AvaloniaFact]
    public async Task FailedPreviousNavigation_PreservesDisplayedImageAndPosition()
    {
        var directory = CreateTestDirectory();
        var first = WriteImage(directory, "first.png");
        var third = WriteImage(directory, "third.png");
        var missing = Path.Combine(directory, "missing.png");
        var dialogs = Substitute.For<IDialogService>();
        var alertCount = 0;
        var initialError = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dialogs.AlertAsync(AppResources.ErrorTitle, AppResources.ImageViewerUnreadable).Returns(_ =>
        {
            Interlocked.Increment(ref alertCount);
            initialError.TrySetResult();
            return Task.CompletedTask;
        });
        var owner = new Window();
        owner.Show();
        var viewer = new ImageViewerWindow(
            [new ImageViewerItem(Guid.NewGuid(), first, "first"), new ImageViewerItem(Guid.NewGuid(), missing, "missing"), new ImageViewerItem(Guid.NewGuid(), third, "third")],
            2,
            owner,
            dialogs);
        var shown = viewer.ShowDialog(owner);
        Dispatcher.UIThread.RunJobs();
        viewer.StartLoading();
        try
        {
            await WaitUntilAsync(() => viewer.DisplayedIndex == 2 || initialError.Task.IsCompleted);
            Assert.Equal(0, Volatile.Read(ref alertCount));
            var displayed = viewer.DisplayedBitmap;

            viewer.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Left });
            await WaitUntilAsync(() => Volatile.Read(ref alertCount) == 1);

            Assert.Equal(2, viewer.DisplayedIndex);
            Assert.Same(displayed, viewer.DisplayedBitmap);
            Assert.Equal(string.Format(System.Globalization.CultureInfo.CurrentCulture, AppResources.ImageViewerPositionFormat, 3, 3), viewer.PositionText);
            viewer.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right });
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, Volatile.Read(ref alertCount));

            viewer.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Left });
            await WaitUntilAsync(() => Volatile.Read(ref alertCount) == 2);
            Assert.Equal(2, viewer.DisplayedIndex);
            Assert.Same(displayed, viewer.DisplayedBitmap);
        }
        finally
        {
            viewer.Close();
            await shown.WaitAsync(TimeSpan.FromSeconds(5));
            viewer.DisposeImage();
            owner.Close();
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task FailedNextNavigation_PreservesDisplayedImageAndPosition()
    {
        var directory = CreateTestDirectory();
        var first = WriteImage(directory, "first.png");
        var third = WriteImage(directory, "third.png");
        var missing = Path.Combine(directory, "missing.png");
        var dialogs = Substitute.For<IDialogService>();
        var alertShown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dialogs.AlertAsync(AppResources.ErrorTitle, AppResources.ImageViewerUnreadable).Returns(_ =>
        {
            alertShown.TrySetResult();
            return Task.CompletedTask;
        });
        var owner = new Window();
        owner.Show();
        var viewer = new ImageViewerWindow(
            [new ImageViewerItem(Guid.NewGuid(), first, "first"), new ImageViewerItem(Guid.NewGuid(), missing, "missing"), new ImageViewerItem(Guid.NewGuid(), third, "third")],
            0,
            owner,
            dialogs);
        var shown = viewer.ShowDialog(owner);
        Dispatcher.UIThread.RunJobs();
        viewer.StartLoading();
        try
        {
            await WaitUntilAsync(() => viewer.DisplayedIndex == 0 || alertShown.Task.IsCompleted);
            Assert.False(alertShown.Task.IsCompleted);
            var displayed = viewer.DisplayedBitmap;

            viewer.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right });
            await alertShown.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0, viewer.DisplayedIndex);
            Assert.Same(displayed, viewer.DisplayedBitmap);
            Assert.Equal(string.Format(System.Globalization.CultureInfo.CurrentCulture, AppResources.ImageViewerPositionFormat, 1, 3), viewer.PositionText);
        }
        finally
        {
            viewer.Close();
            await shown.WaitAsync(TimeSpan.FromSeconds(5));
            viewer.DisposeImage();
            owner.Close();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTestDirectory()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "tmp", "image-viewer-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string WriteImage(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllBytes(path, Convert.FromBase64String(MultiPixelPngBase64));
        return path;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, timeout.Token);
        }
    }
}

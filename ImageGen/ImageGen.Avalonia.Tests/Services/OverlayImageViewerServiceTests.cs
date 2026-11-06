using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ImageGen.Avalonia.Services;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using NSubstitute;

namespace ImageGen.Avalonia.Tests.Services;

public sealed class OverlayImageViewerServiceTests
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAMCAIAAADQ/GvKAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAARSURBVChTY/iPA4xKoIH//wEiRB7wcYTgRwAAAABJRU5ErkJggg==";

    [AvaloniaFact]
    public async Task ShowAsync_LoadsAndCloseButtonRemovesPanel()
    {
        var directory = CreateDirectory();
        try
        {
            var first = WriteImage(directory, "first.png");
            var second = WriteImage(directory, "second.png");
            var (owner, host, service) = CreateService();
            var showing = service.ShowAsync(Items(first, second), 0, CancellationToken.None);
            var panel = Assert.IsType<ImageViewerPanel>(Assert.Single(host.Children));
            await WaitUntilAsync(() => panel.DisplayedIndex == 0);

            Assert.Equal(string.Format(System.Globalization.CultureInfo.CurrentCulture, AppResources.ImageViewerPositionFormat, 1, 2), panel.PositionText);
            ClickButton(panel, AppResources.ImageViewerNext);
            await WaitUntilAsync(() => panel.DisplayedIndex == 1);
            Assert.Equal(string.Format(System.Globalization.CultureInfo.CurrentCulture, AppResources.ImageViewerPositionFormat, 2, 2), panel.PositionText);
            ClickButton(panel, AppResources.ImageViewerClose);

            await showing.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Empty(host.Children);
            Assert.Null(panel.DisplayedBitmap);
            owner.Close();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task ShowAsync_EscapeClosesPanel()
    {
        var directory = CreateDirectory();
        try
        {
            var image = WriteImage(directory, "image.png");
            var (owner, host, service) = CreateService();
            var showing = service.ShowAsync(Items(image), 0, CancellationToken.None);
            var panel = Assert.IsType<ImageViewerPanel>(Assert.Single(host.Children));
            await WaitUntilAsync(() => panel.DisplayedIndex == 0);

            panel.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            await showing.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Empty(host.Children);
            owner.Close();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task TryCloseTop_ClosesPanelAndReturnsFalseWhenIdle()
    {
        var directory = CreateDirectory();
        try
        {
            var image = WriteImage(directory, "image.png");
            var (owner, host, service) = CreateService();
            Assert.False(service.TryCloseTop());
            var showing = service.ShowAsync(Items(image), 0, CancellationToken.None);
            _ = Assert.IsType<ImageViewerPanel>(Assert.Single(host.Children));

            Assert.True(service.TryCloseTop());
            await showing.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Empty(host.Children);
            Assert.False(service.TryCloseTop());
            owner.Close();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task ShowAsync_CancelledTokenClosesPanel()
    {
        var directory = CreateDirectory();
        try
        {
            var image = WriteImage(directory, "image.png");
            var (owner, host, service) = CreateService();
            using var cancellation = new CancellationTokenSource();
            var showing = service.ShowAsync(Items(image), 0, cancellation.Token);
            _ = Assert.IsType<ImageViewerPanel>(Assert.Single(host.Children));

            await cancellation.CancelAsync();
            await showing.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Empty(host.Children);
            owner.Close();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task ShowAsync_MissingImageAlertsAndClosesPanel()
    {
        var directory = CreateDirectory();
        try
        {
            var dialogs = Substitute.For<IDialogService>();
            var alerted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            dialogs.AlertAsync(AppResources.ErrorTitle, AppResources.ImageViewerUnreadable).Returns(_ =>
            {
                alerted.TrySetResult();
                return Task.CompletedTask;
            });
            var (owner, host, service) = CreateService(dialogs);
            var showing = service.ShowAsync(Items(Path.Combine(directory, "missing.png")), 0, CancellationToken.None);

            await alerted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await showing.WaitAsync(TimeSpan.FromSeconds(5));

            await dialogs.Received(1).AlertAsync(AppResources.ErrorTitle, AppResources.ImageViewerUnreadable);
            Assert.Empty(host.Children);
            owner.Close();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task ShowAsync_AlreadyOpen_IgnoresSecondCall()
    {
        var directory = CreateDirectory();
        try
        {
            var image = WriteImage(directory, "image.png");
            var (owner, host, service) = CreateService();
            var first = service.ShowAsync(Items(image), 0, CancellationToken.None);
            var panel = Assert.IsType<ImageViewerPanel>(Assert.Single(host.Children));
            await WaitUntilAsync(() => panel.DisplayedIndex == 0);

            await service.ShowAsync(Items(image), 0, CancellationToken.None);
            Assert.Single(host.Children);
            ClickButton(panel, AppResources.ImageViewerClose);
            await first.WaitAsync(TimeSpan.FromSeconds(5));
            owner.Close();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static (Window Owner, Grid Host, OverlayImageViewerService Service) CreateService(IDialogService? dialogs = null)
    {
        var host = new Grid();
        var owner = new Window { Content = host };
        owner.Show();
        return (owner, host, new OverlayImageViewerService(() => host, dialogs ?? Substitute.For<IDialogService>()));
    }

    private static IReadOnlyList<ImageViewerItem> Items(params string[] paths) => paths
        .Select((path, index) => new ImageViewerItem(Guid.NewGuid(), path, $"Image {index + 1}"))
        .ToArray();

    private static void ClickButton(Control root, string name)
    {
        var button = Assert.Single(root.GetVisualDescendants().OfType<Button>(), candidate => AutomationProperties.GetName(candidate) == name);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static string CreateDirectory()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "tmp", "overlay-image-viewer-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string WriteImage(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllBytes(path, Convert.FromBase64String(Png));
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

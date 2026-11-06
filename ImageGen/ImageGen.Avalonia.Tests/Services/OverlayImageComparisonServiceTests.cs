using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using ImageGen.Avalonia.Services;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using NSubstitute;

namespace ImageGen.Avalonia.Tests.Services;

public sealed class OverlayImageComparisonServiceTests
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAMCAIAAADQ/GvKAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAARSURBVChTY/iPA4xKoIH//wEiRB7wcYTgRwAAAABJRU5ErkJggg==";

    [AvaloniaFact]
    public async Task ShowAsync_LoadsImagesAndCloseRemovesPanel()
    {
        var directory = CreateImages(out var first, out var second);
        try
        {
            var (host, service) = CreateService();
            var model = Model(first, second);
            var shown = service.ShowAsync(model, CancellationToken.None);
            await WaitUntilAsync(() => host.Children.OfType<ComparisonPanel>().Any(panel => panel.FirstBitmap is not null && panel.SecondBitmap is not null));
            var panel = Assert.IsType<ComparisonPanel>(Assert.Single(host.Children));

            Assert.False(panel.IsOverlayLayout);
            model.Mode = ComparisonMode.Overlay;
            Dispatcher.UIThread.RunJobs();
            Assert.True(panel.IsOverlayLayout);
            panel.RequestClose();
            await shown.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Empty(host.Children);
            Assert.Null(panel.FirstBitmap);
            Assert.Null(panel.SecondBitmap);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task TryCloseTop_ClosesActivePanelAndReturnsFalseWhenIdle()
    {
        var directory = CreateImages(out var first, out var second);
        try
        {
            var (host, service) = CreateService();
            Assert.False(service.TryCloseTop());
            var shown = service.ShowAsync(Model(first, second), CancellationToken.None);
            await WaitUntilAsync(() => host.Children.Count == 1);

            Assert.True(service.TryCloseTop());
            await shown.WaitAsync(TimeSpan.FromSeconds(5));
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(host.Children);
            Assert.False(service.TryCloseTop());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task Cancellation_RemovesPanelAndDisposesImages()
    {
        var directory = CreateImages(out var first, out var second);
        try
        {
            var (host, service) = CreateService();
            using var cancellation = new CancellationTokenSource();
            var shown = service.ShowAsync(Model(first, second), cancellation.Token);
            await WaitUntilAsync(() => host.Children.OfType<ComparisonPanel>().Any(panel => panel.FirstBitmap is not null && panel.SecondBitmap is not null));
            var panel = Assert.IsType<ComparisonPanel>(Assert.Single(host.Children));

            cancellation.Cancel();
            await shown.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Empty(host.Children);
            Assert.Null(panel.FirstBitmap);
            Assert.Null(panel.SecondBitmap);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task MissingImage_ShowsLocalizedAlertAndRemovesPanel()
    {
        var dialogs = Substitute.For<IDialogService>();
        var alerted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dialogs.AlertAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(call =>
        {
            Assert.Equal(AppResources.ErrorTitle, call.ArgAt<string>(0));
            Assert.Equal(AppResources.ImageViewerUnreadable, call.ArgAt<string>(1));
            alerted.TrySetResult();
            return Task.CompletedTask;
        });
        var host = new Grid();
        var service = new OverlayImageComparisonService(() => host, dialogs);
        var shown = service.ShowAsync(Model("/missing/first.png", "/missing/second.png"), CancellationToken.None);
        await alerted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await shown.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(host.Children);
        await dialogs.Received(1).AlertAsync(AppResources.ErrorTitle, AppResources.ImageViewerUnreadable);
    }

    [AvaloniaFact]
    public async Task Escape_ClosesPanel()
    {
        var directory = CreateImages(out var first, out var second);
        try
        {
            var (host, service) = CreateService();
            var shown = service.ShowAsync(Model(first, second), CancellationToken.None);
            await WaitUntilAsync(() => host.Children.OfType<ComparisonPanel>().Any());
            var panel = Assert.IsType<ComparisonPanel>(Assert.Single(host.Children));

            panel.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            await shown.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Empty(host.Children);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static (Grid Host, OverlayImageComparisonService Service) CreateService()
    {
        var host = new Grid();
        return (host, new OverlayImageComparisonService(() => host, Substitute.For<IDialogService>()));
    }

    private static ComparisonPageModel Model(string first, string second) => new(
        new ComparisonImage(Guid.NewGuid(), first, "model A", "seed 1", "$0.1"),
        new ComparisonImage(Guid.NewGuid(), second, "model B", "seed 2", "unknown"));

    private static string CreateImages(out string first, out string second)
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "tmp", "overlay-comparison-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        first = Path.Combine(directory, "first.png");
        second = Path.Combine(directory, "second.png");
        File.WriteAllBytes(first, Convert.FromBase64String(Png));
        File.WriteAllBytes(second, Convert.FromBase64String(Png));
        return directory;
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

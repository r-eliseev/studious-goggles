using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using ImageGen.Avalonia.Services;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
using NSubstitute;

namespace ImageGen.Avalonia.Tests.Services;

public sealed class ComparisonWindowTests
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAMCAIAAADQ/GvKAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAARSURBVChTY/iPA4xKoIH//wEiRB7wcYTgRwAAAABJRU5ErkJggg==";

    [AvaloniaFact]
    public async Task WindowLoadsBothImages_SharedZoomAndClosesCleanly()
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "tmp", "comparison-window-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var firstPath = Path.Combine(directory, "first.png");
        var secondPath = Path.Combine(directory, "second.png");
        File.WriteAllBytes(firstPath, Convert.FromBase64String(Png));
        File.WriteAllBytes(secondPath, Convert.FromBase64String(Png));
        var model = Model(firstPath, secondPath);
        var owner = new Window();
        owner.Show();
        var window = new ComparisonWindow(model, owner, Substitute.For<IDialogService>());
        var shown = window.ShowDialog(owner);
        Dispatcher.UIThread.RunJobs();
        window.StartLoading();
        try
        {
            await WaitUntilAsync(() => window.FirstBitmap is not null && window.SecondBitmap is not null);
            model.Mode = ComparisonMode.Overlay;
            model.SetZoom(3);
            model.SetOffset(0.25, -0.2);
            model.SliderFraction = 0.7;
            Dispatcher.UIThread.RunJobs();

            Assert.True(window.IsOverlayLayout);
            Assert.Equal(3, window.FirstScale);
            Assert.Equal(0.7, model.SliderFraction);
            Assert.NotNull(window.FirstBitmap);
            Assert.NotNull(window.SecondBitmap);
        }
        finally
        {
            window.Close();
            await shown.WaitAsync(TimeSpan.FromSeconds(5));
            window.DisposeImages();
            owner.Close();
            Directory.Delete(directory, recursive: true);
        }
        Assert.Null(window.FirstBitmap);
        Assert.Null(window.SecondBitmap);
    }

    [AvaloniaFact]
    public async Task MissingImage_ShowsMessageAndCloses()
    {
        var dialogs = Substitute.For<IDialogService>();
        var alerted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dialogs.AlertAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(_ => { alerted.TrySetResult(); return Task.CompletedTask; });
        var owner = new Window();
        owner.Show();
        var window = new ComparisonWindow(Model("/missing/first.png", "/missing/second.png"), owner, dialogs);
        var shown = window.ShowDialog(owner);
        Dispatcher.UIThread.RunJobs();
        window.StartLoading();
        await alerted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Dispatcher.UIThread.RunJobs();
        await shown.WaitAsync(TimeSpan.FromSeconds(5));
        await dialogs.Received(1).AlertAsync(Arg.Any<string>(), Arg.Any<string>());
        window.DisposeImages();
        owner.Close();
    }

    private static ComparisonPageModel Model(string first, string second) => new(
        new ComparisonImage(Guid.NewGuid(), first, "model A", "seed 1", "$0.1"),
        new ComparisonImage(Guid.NewGuid(), second, "model B", "seed 2", "unknown"));

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

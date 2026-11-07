using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using ImageGen.Avalonia.Services;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Storage;
using ImageGen.Presentation.Appearance;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ImageGen.Avalonia.Tests.Views;

public sealed class GenerationCardMetadataTests
{
    [AvaloniaFact]
    public void QuietStyle_ShowsImageDimensions()
    {
        AssertMetadataVisible(AppearanceStyle.Quiet);
    }

    [AvaloniaFact]
    public void GalleryStyle_ShowsImageDimensions()
    {
        AssertMetadataVisible(AppearanceStyle.Gallery);
    }

    [AvaloniaFact]
    public void NotebookStyle_ShowsImageDimensions()
    {
        AssertMetadataVisible(AppearanceStyle.Notebook);
    }

    [AvaloniaFact]
    public void VideoCard_ShowsPlaceholderAndDurationWithoutImageViewer()
    {
        var provider = ((ImageGen.Avalonia.App)Application.Current!).Services!;
        var chat = provider.GetRequiredService<ChatPageModel>();
        var generation = new Generation
        {
            Id = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ProviderId = "openrouter",
            ModelId = "vendor/video",
            Prompt = "a fox",
            Seed = 1,
            SeedApplied = false,
            Status = GenerationStatus.Succeeded,
            Kind = MediaKind.Video,
            ImagePath = "media/session/video.mp4",
            MediaType = "video/mp4",
            DurationSeconds = 8,
            CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            SortOrder = 0,
        };
        var card = new GenerationItemViewModel(
            generation, Substitute.For<IImageFileStore>(), Substitute.For<IDialogService>(),
            Substitute.For<IClipboardService>(), Substitute.For<IImageSaver>(), _ => Task.CompletedTask,
            _ => Task.CompletedTask, _ => Task.CompletedTask, (_, _) => Task.CompletedTask, _ => Task.CompletedTask);
        var run = new RunItemViewModel(
            new Run
            {
                Id = generation.RunId,
                SessionId = generation.SessionId,
                Prompt = generation.Prompt,
                Origin = RunOrigin.Assistant,
                CreatedAt = generation.CreatedAt
            }, [card],
            new CopyTextAction(Substitute.For<IClipboardService>(), Substitute.For<IMainThread>(), generation.Prompt));
        chat.Items.Clear();
        chat.Items.Add(run);
        var view = new ChatView { DataContext = chat };
        var window = new MainWindow { Width = 1100, Height = 800 };
        window.PageArea.Content = view;
        window.Show();
        window.UpdateLayout();

        Assert.True(card.IsVideo);
        Assert.Equal("8 s", card.DurationText);
        Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == card.DurationText && text.IsVisible);
        Assert.False(card.HasImage);
    }

    private static void AssertMetadataVisible(AppearanceStyle style)
    {
        using var restore = ApplyStyle(style);
        var provider = ((ImageGen.Avalonia.App)Application.Current!).Services!;
        var chat = provider.GetRequiredService<ChatPageModel>();
        var generation = new Generation
        {
            Id = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ProviderId = "openrouter",
            ModelId = "vendor/model",
            Prompt = "a fox",
            Seed = 1,
            SeedApplied = true,
            Status = GenerationStatus.Succeeded,
            ImagePath = "images/session/image.png",
            CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            SortOrder = 0,
        };
        var card = new GenerationItemViewModel(
            generation,
            Substitute.For<IImageFileStore>(),
            Substitute.For<IDialogService>(),
            Substitute.For<IClipboardService>(),
            Substitute.For<IImageSaver>(),
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            (_, _) => Task.CompletedTask,
            _ => Task.CompletedTask);
        card.ApplyImageDimensions(new ImageDimensions(1200, 800));
        var run = new RunItemViewModel(
            new Run { Id = generation.RunId, SessionId = generation.SessionId, Prompt = generation.Prompt, Origin = RunOrigin.Assistant, CreatedAt = generation.CreatedAt },
            [card],
            new CopyTextAction(Substitute.For<IClipboardService>(), Substitute.For<IMainThread>(), generation.Prompt));
        chat.Items.Clear();
        chat.Items.Add(run);
        var view = new ChatView { DataContext = chat };
        var window = new MainWindow { Width = 1100, Height = 800 };
        window.PageArea.Content = view;
        window.Show();
        window.UpdateLayout();

        var label = Assert.Single(view.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == card.ImageDimensionsText);
        Assert.True(label.IsVisible);
        var image = Assert.Single(view.GetVisualDescendants().OfType<Image>());
        Assert.Equal(Stretch.Uniform, image.Stretch);
    }

    private static IDisposable ApplyStyle(AppearanceStyle style)
    {
        var app = (Application)Application.Current!;
        var service = ((ImageGen.Avalonia.App)app).Services!.GetRequiredService<AppearanceService>();
        new AvaloniaAppearanceApplier(service, app).Apply(new AppearanceSettings(style, AppearanceTheme.Light));
        return new Restorer(() => new AvaloniaAppearanceApplier(service, app).Apply(AppearanceSettings.Default));
    }

    private sealed class Restorer(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}

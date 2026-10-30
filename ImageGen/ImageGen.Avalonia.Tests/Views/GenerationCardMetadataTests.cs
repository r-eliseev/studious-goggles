using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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

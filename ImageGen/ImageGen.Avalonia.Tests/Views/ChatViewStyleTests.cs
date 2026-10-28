using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.VisualTree;
using ImageGen.Avalonia.Services;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Storage;
using ImageGen.Presentation.Appearance;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ImageGen.Avalonia.Tests.Views;

public class ChatViewStyleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [AvaloniaFact]
    public void GalleryStyle_ResourceIsRightAlignment()
    {
        using var restore = ApplyStyle(AppearanceStyle.Gallery);

        Assert.True(Application.Current!.TryFindResource("IgUserMessageAlignment", out var aligned));
        Assert.Equal(HorizontalAlignment.Right, (HorizontalAlignment)aligned!);
    }

    [AvaloniaFact]
    public void GalleryStyle_UserMessageAlignedRight()
    {
        using var restore = ApplyStyle(AppearanceStyle.Gallery);
        var (view, chat) = CreateView();
        chat.Items.Add(UserMessage("draw a red fox"));
        Render(view);

        var bubble = view.GetVisualDescendants().OfType<Border>()
            .First(border => border.Child is StackPanel panel
                && panel.Children.OfType<TextBlock>().Any(text => text.Text == AppResources.UserRole));

        Assert.Equal(HorizontalAlignment.Right, bubble.HorizontalAlignment);
    }

    [AvaloniaFact]
    public void NotebookStyle_RunHasBorder()
    {
        using var restore = ApplyStyle(AppearanceStyle.Notebook);
        var (view, chat) = CreateView();
        chat.Items.Add(Run(GenerationStatus.Succeeded));
        Render(view);

        var runBorder = view.GetVisualDescendants().OfType<Border>()
            .First(border => border.Child is StackPanel panel
                && panel.Children.OfType<TextBlock>().Any(text => text.Text?.Contains("done", StringComparison.Ordinal) == true));

        Assert.True(runBorder.BorderThickness.Top > 0);
    }

    [AvaloniaFact]
    public void FilteredCard_ShowsFilteredText()
    {
        using var restore = ApplyStyle(AppearanceStyle.Quiet);
        var (view, chat) = CreateView();
        chat.Items.Add(Run(GenerationStatus.Filtered));
        Render(view);

        Assert.Contains(
            view.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == AppResources.FilteredNotice);
    }

    [AvaloniaFact]
    public void QueuedCard_ShowsQueuedStatus()
    {
        using var restore = ApplyStyle(AppearanceStyle.Quiet);
        var (view, chat) = CreateView();
        chat.Items.Add(Run(GenerationStatus.Queued));
        Render(view);

        Assert.Contains(
            view.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == AppResources.StatusQueued);
    }

    private static (ChatView View, ChatPageModel Chat) CreateView()
    {
        var provider = ((ImageGen.Avalonia.App)Application.Current!).Services!;
        var chat = provider.GetRequiredService<ChatPageModel>();
        var view = new ChatView { DataContext = chat };
        return (view, chat);
    }

    private static void Render(ChatView view)
    {
        // A top-level window so DynamicResource lookups reach the applied style dictionary in Application.Resources.
        var window = new MainWindow { Width = 1100, Height = 800 };
        window.PageArea.Content = view;
        window.Show();
        window.UpdateLayout();
    }

    private static MessageItemViewModel UserMessage(string text)
    {
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Role = ChatRole.User,
            Text = text,
            CreatedAt = Now,
        };
        return new MessageItemViewModel(message, text, Copy());
    }

    private static RunItemViewModel Run(GenerationStatus status)
    {
        var run = new Run
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Prompt = "red fox",
            Origin = RunOrigin.Assistant,
            CreatedAt = Now,
        };
        return new RunItemViewModel(run, [Card(status)], Copy());
    }

    private static GenerationItemViewModel Card(GenerationStatus status)
    {
        var generation = new Generation
        {
            Id = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ProviderId = "openrouter",
            ModelId = "vendor/model",
            Prompt = "red fox",
            Seed = 1,
            SeedApplied = true,
            Status = status,
            CostUsd = status == GenerationStatus.Succeeded ? 0.014m : null,
            CreatedAt = Now,
            SortOrder = 0,
        };
        return new GenerationItemViewModel(
            generation,
            Substitute.For<IImageFileStore>(),
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            (_, _) => Task.CompletedTask);
    }

    private static CopyTextAction Copy() =>
        new(Substitute.For<IClipboardService>(), Substitute.For<IMainThread>(), "text");

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

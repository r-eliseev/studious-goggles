using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Storage;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ImageGen.Avalonia.Tests.Views;

/// <summary>
/// The actions of a generation card (task 069): the icon row is hidden until the card is hovered or has keyboard focus,
/// the context menu lists every action, and each icon button has an accessible name.
/// </summary>
public class CardActionsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [AvaloniaFact]
    public void Actions_HiddenWithoutPointer()
    {
        var (view, chat) = CreateView();
        chat.Items.Add(Run(GenerationStatus.Succeeded));
        Render(view);

        var actions = CardActions(view);

        Assert.False(actions.IsVisible);
    }

    [AvaloniaFact]
    public void Actions_VisibleOnFocusWithin()
    {
        var (view, chat) = CreateView();
        chat.Items.Add(Run(GenerationStatus.Succeeded));
        Render(view);

        var card = view.GetVisualDescendants().OfType<Border>()
            .First(border => border.Classes.Contains("card"));
        var actions = CardActions(view);

        // Keyboard focus lands on the card itself; ":focus-within" then reveals the hidden action row.
        card.Focusable = true;
        card.Focus();
        view.UpdateLayout();

        Assert.True(actions.IsVisible);
    }

    [AvaloniaFact]
    public void ContextMenu_HasAllActions()
    {
        var (view, chat) = CreateView();
        chat.Items.Add(Run(GenerationStatus.Succeeded));
        Render(view);

        var card = view.GetVisualDescendants().OfType<Border>()
            .First(border => border.Classes.Contains("card"));
        var menu = card.ContextMenu!;
        var headers = menu.Items.OfType<MenuItem>().Select(item => item.Header as string).ToList();

        Assert.Equal(
            new[]
            {
                AppResources.Repeat,
                AppResources.NewSeed,
                AppResources.ActionEditPrompt,
                AppResources.ActionDescribeImage,
                AppResources.ActionPromptFromImage,
                AppResources.CopyPrompt,
                AppResources.OtherModel,
                AppResources.ErrorDetailsTitle,
            },
            headers);
    }

    [AvaloniaFact]
    public void IconButtons_HaveAutomationNames()
    {
        var (view, chat) = CreateView();
        chat.Items.Add(Run(GenerationStatus.Succeeded));
        Render(view);

        var actions = CardActions(view);
        // A succeeded card hides the failure-only Details icon; only the visible buttons are named here.
        var names = actions.GetVisualDescendants().OfType<Button>()
            .Where(button => button.IsVisible)
            .Select(AutomationProperties.GetName)
            .ToList();

        Assert.Equal(
            new[]
            {
                AppResources.Repeat,
                AppResources.NewSeed,
                AppResources.Edit,
                AppResources.DescribeButton,
                AppResources.CopyPrompt,
                AppResources.More,
            },
            names);
    }

    private static StackPanel CardActions(ChatView view) =>
        view.GetVisualDescendants().OfType<StackPanel>()
            .First(panel => panel.Classes.Contains("card-actions"));

    private static (ChatView View, ChatPageModel Chat) CreateView()
    {
        var provider = ((ImageGen.Avalonia.App)Application.Current!).Services!;
        var chat = provider.GetRequiredService<ChatPageModel>();
        var view = new ChatView { DataContext = chat };
        return (view, chat);
    }

    private static void Render(ChatView view)
    {
        var window = new MainWindow { Width = 1100, Height = 800 };
        window.PageArea.Content = view;
        window.Show();
        window.UpdateLayout();
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
            Substitute.For<IDialogService>(),
            Substitute.For<IClipboardService>(),
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            (_, _) => Task.CompletedTask,
            _ => Task.CompletedTask);
    }

    private static CopyTextAction Copy() =>
        new(Substitute.For<IClipboardService>(), Substitute.For<IMainThread>(), "text");
}

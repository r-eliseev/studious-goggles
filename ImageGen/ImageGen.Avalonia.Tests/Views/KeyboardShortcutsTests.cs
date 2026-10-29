using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Avalonia.Services;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Storage;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ImageGen.Avalonia.Tests.Views;

public class KeyboardShortcutsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private static KeyBinding? BindingOf(Window window, Key key) =>
        window.KeyBindings.OfType<KeyBinding>().SingleOrDefault(binding => Equals(binding.Gesture, new KeyGesture(key, PlatformKeys.Primary)));

    [AvaloniaFact]
    public async Task CtrlN_IsBoundToNewSession()
    {
        using var shell = new ShellHarness();
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        shell.Window.Show();
        var sessions = shell.Provider.GetRequiredService<SessionsPageModel>();

        Assert.Same(sessions.NewSessionCommand, BindingOf(shell.Window, Key.N)!.Command);
    }

    [AvaloniaFact]
    public async Task CtrlComma_IsBoundToSettings()
    {
        using var shell = new ShellHarness();
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        shell.Window.Show();
        var sessions = shell.Provider.GetRequiredService<SessionsPageModel>();

        Assert.Same(sessions.OpenSettingsCommand, BindingOf(shell.Window, Key.OemComma)!.Command);
    }

    [AvaloniaFact]
    public async Task CtrlF_PutsCursorInSearchField()
    {
        using var shell = new ShellHarness();
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        shell.Window.Show();
        await shell.Navigation.GoToAsync(Routes.Sessions);
        shell.Window.UpdateLayout();

        await ((IAsyncRelayCommand)BindingOf(shell.Window, Key.F)!.Command!).ExecuteAsync(null);
        shell.Window.UpdateLayout();

        var search = shell.Window.PageArea.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "SearchBox");
        Assert.True(search.IsFocused);
    }

    [AvaloniaFact]
    public async Task RKey_OnCard_IsBoundToRepeatOfThatCard()
    {
        using var shell = new ShellHarness();
        shell.Window.Show();
        var chat = await shell.OpenNewChatAsync();
        var card = Card();
        chat.Items.Add(new RunItemViewModel(Run(), [card], Copy()));
        shell.Window.UpdateLayout();

        var cardBorder = shell.Window.PageArea.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("card"));
        Assert.Contains(
            cardBorder.KeyBindings.OfType<KeyBinding>(),
            binding => Equals(binding.Gesture, new KeyGesture(Key.R)) && ReferenceEquals(binding.Command, card.RepeatCommand));
    }

    [AvaloniaFact]
    public async Task InputBox_IsOutsideAnyCard_SoTypingRDoesNotRepeat()
    {
        using var shell = new ShellHarness();
        shell.Window.Show();
        await shell.OpenNewChatAsync();
        shell.Window.UpdateLayout();

        var input = shell.Window.PageArea.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "Input");
        Assert.DoesNotContain(input.GetVisualAncestors().OfType<Border>(), border => border.Classes.Contains("card"));
    }

    private static GenerationItemViewModel Card()
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
            Status = GenerationStatus.Queued,
            CreatedAt = Now,
            SortOrder = 0,
        };
        return new GenerationItemViewModel(
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
    }

    private static Run Run() => new()
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Prompt = "red fox",
        Origin = RunOrigin.Assistant,
        CreatedAt = Now,
    };

    private static CopyTextAction Copy() =>
        new(Substitute.For<IClipboardService>(), Substitute.For<IMainThread>(), "red fox");
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Tests.Views;

public class TranscriptScrollTests
{
    [AvaloniaFact]
    public async Task OpenChat_LongHistory_TranscriptAtEnd()
    {
        using var shell = new ShellHarness();
        var (view, _) = await OpenLongChatAsync(shell);
        shell.Window.UpdateLayout();

        await shell.WaitUntilAsync(() => AtEnd(GetTranscript(view)), "лента в конце длинной истории");

        Assert.True(GetTranscript(view).Offset.Y > 0);
    }

    [AvaloniaFact]
    public async Task ScrolledUp_WindowResized_StaysUp()
    {
        using var shell = new ShellHarness();
        var (view, _) = await OpenLongChatAsync(shell);
        var transcript = GetTranscript(view);
        await shell.WaitUntilAsync(() => AtEnd(transcript), "лента в конце");

        transcript.Offset = new Vector(0, 0);
        shell.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        shell.Window.Height = 500;
        shell.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, transcript.Offset.Y);
    }

    [AvaloniaFact]
    public async Task ScrolledUp_Reload_ScrollsToEnd()
    {
        using var shell = new ShellHarness();
        var (view, chat) = await OpenLongChatAsync(shell);
        var transcript = GetTranscript(view);
        await shell.WaitUntilAsync(() => AtEnd(transcript), "лента в конце");

        transcript.Offset = new Vector(0, 0);
        shell.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        await chat.ReloadAsync();

        await shell.WaitUntilAsync(() => AtEnd(transcript), "лента возвращена в конец после обновления строк");
    }

    [AvaloniaFact]
    public async Task AtEnd_WindowShrunk_StaysAtEnd()
    {
        using var shell = new ShellHarness();
        var (view, _) = await OpenLongChatAsync(shell);
        var transcript = GetTranscript(view);
        await shell.WaitUntilAsync(() => AtEnd(transcript), "лента в конце");

        shell.Window.Height = 400;
        shell.Window.UpdateLayout();

        await shell.WaitUntilAsync(() => AtEnd(transcript), "лента остаётся в конце после уменьшения окна");
    }

    [AvaloniaFact]
    public async Task LastRun_GrowsWhileFollowing_TranscriptStaysAtEnd()
    {
        using var shell = new ShellHarness();
        var (view, chat) = await OpenLongChatAsync(shell);
        var transcript = GetTranscript(view);
        await shell.WaitUntilAsync(() => AtEnd(transcript), "лента в конце");
        var run = AddRun(chat, shell);
        shell.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        await shell.WaitUntilAsync(() => AtEnd(transcript), "новый прогон в конце");

        var card = Assert.Single(run.Generations);
        card.NoticeText = new string('x', 5000);
        card.HasNotice = true;
        shell.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        await shell.WaitUntilAsync(() => AtEnd(transcript), "нижний край выросшей карточки виден");
    }

    [AvaloniaFact]
    public async Task LastRun_GrowsAfterScrollUp_TranscriptStaysUp()
    {
        using var shell = new ShellHarness();
        var (view, chat) = await OpenLongChatAsync(shell);
        var transcript = GetTranscript(view);
        var run = AddRun(chat, shell);
        shell.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        await shell.WaitUntilAsync(() => AtEnd(transcript), "новый прогон в конце");

        transcript.Offset = new Vector(0, 0);
        shell.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var card = Assert.Single(run.Generations);
        card.NoticeText = new string('x', 5000);
        card.HasNotice = true;
        shell.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, transcript.Offset.Y);
    }

    [AvaloniaFact]
    public async Task DisappearingBeforePendingScroll_DoesNotScrollOldView()
    {
        using var shell = new ShellHarness();
        var (view, chat) = await OpenLongChatAsync(shell);
        var transcript = GetTranscript(view);
        await shell.WaitUntilAsync(() => AtEnd(transcript), "лента в конце");
        transcript.Offset = new Vector(0, 0);
        shell.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        chat.Items.Add(chat.Items[0]);
        view.OnDisappearing();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, transcript.Offset.Y);
    }

    [AvaloniaFact]
    public async Task OnAppearingRepeated_DoesNotDuplicateScrollReaction()
    {
        using var shell = new ShellHarness();
        var (view, chat) = await OpenLongChatAsync(shell);
        view.OnAppearing();
        view.OnAppearing();
        await shell.WaitUntilAsync(() => chat.Items.Count == 40, "повторная загрузка ленты");
        shell.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var transcript = GetTranscript(view);
        await shell.WaitUntilAsync(() => AtEnd(transcript), "лента в конце после повторного появления");

        transcript.Offset = new Vector(0, 0);
        shell.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        chat.Items.Add(chat.Items[0]);

        await shell.WaitUntilAsync(() => AtEnd(transcript), "одно изменение строк прокручивает в конец");
    }

    private static RunItemViewModel AddRun(ChatPageModel chat, ShellHarness shell)
    {
        var now = DateTimeOffset.UtcNow;
        var run = new Run
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Prompt = "a red fox",
            Origin = RunOrigin.Assistant,
            CreatedAt = now,
        };
        var generation = new Generation
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            SessionId = run.SessionId,
            ProviderId = ProviderIds.OpenRouter,
            ModelId = "vendor/image",
            Prompt = run.Prompt,
            Seed = 42,
            SeedApplied = true,
            Status = GenerationStatus.Running,
            CreatedAt = now,
            SortOrder = 0,
        };
        var clipboard = shell.Provider.GetRequiredService<IClipboardService>();
        var mainThread = shell.Provider.GetRequiredService<IMainThread>();
        var card = new GenerationItemViewModel(
            generation,
            shell.Provider.GetRequiredService<IImageFileStore>(),
            shell.Dialogs,
            clipboard,
            shell.Provider.GetRequiredService<IImageSaver>(),
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            (_, _) => Task.CompletedTask,
            _ => Task.CompletedTask);
        var item = new RunItemViewModel(run, [card], new CopyTextAction(clipboard, mainThread, run.Prompt));
        chat.Items.Add(item);
        return item;
    }

    private static async Task<(ChatView View, ChatPageModel Model)> OpenLongChatAsync(ShellHarness shell)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        var session = await shell.Provider.GetRequiredService<SessionService>().CreateAsync(cancellationToken);
        var messages = shell.Provider.GetRequiredService<IChatMessageRepository>();
        for (var index = 0; index < 40; index++)
        {
            await messages.AddAsync(new ChatMessage
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Role = index % 2 == 0 ? ChatRole.User : ChatRole.Assistant,
                Text = $"message {index}",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(index),
            }, cancellationToken);
        }

        shell.Window.Show();
        await shell.Navigation.GoToAsync(Routes.ChatFor(session.Id));
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is ChatView, "чат открыт");
        var view = (ChatView)shell.Window.PageArea.Content!;
        var model = (ChatPageModel)view.DataContext!;
        await shell.WaitUntilAsync(() => model.Items.Count == 40, "40 сообщений загружены");
        shell.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return (view, model);
    }

    private static ScrollViewer GetTranscript(ChatView view) => view.FindControl<ScrollViewer>("Transcript")!;

    private static bool AtEnd(ScrollViewer transcript) =>
        TranscriptFollower.IsAtEnd(transcript.Offset.Y, transcript.Viewport.Height, transcript.Extent.Height);
}

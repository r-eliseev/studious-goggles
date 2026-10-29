using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Tests.Views;

public class SummaryMarkerTests
{
    [AvaloniaFact]
    public async Task ChatWithSummary_ShowsMarkerWithShowSummaryButton()
    {
        var ct = TestContext.Current.CancellationToken;
        using var shell = new ShellHarness();
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        var session = await shell.Provider.GetRequiredService<SessionService>().CreateAsync(ct);
        var covered = new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = ChatRole.User,
            Text = "a red fox",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        };
        await shell.Provider.GetRequiredService<IChatMessageRepository>().AddAsync(covered, ct);
        await shell.Provider.GetRequiredService<IConversationSummaryRepository>().UpsertAsync(new ConversationSummary
        {
            SessionId = session.Id,
            Text = "The user wants a red fox.",
            CoversUpToMessageId = covered.Id,
            Model = new ModelRef(ProviderIds.OpenRouter, "vendor/assistant"),
            CreatedAt = DateTimeOffset.UtcNow,
        }, ct);
        shell.Window.Show();

        await shell.Navigation.GoToAsync(Routes.ChatFor(session.Id));
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is ChatView, "чат открыт");
        var chat = (ChatPageModel)((Control)shell.Window.PageArea.Content!).DataContext!;
        await shell.WaitUntilAsync(() => chat.Items.OfType<SummaryMarkerViewModel>().Any(), "разделитель в ленте");
        shell.Window.UpdateLayout();

        var view = Assert.IsType<ChatView>(shell.Window.PageArea.Content);
        var names = view.GetVisualDescendants().OfType<Button>().Select(AutomationProperties.GetName).ToList();
        Assert.Contains(AppResources.ShowSummary, names);
    }
}

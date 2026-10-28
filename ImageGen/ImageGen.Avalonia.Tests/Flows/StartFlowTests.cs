using System.Net;
using Avalonia.Headless.XUnit;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Services;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ImageGen.Avalonia.Tests.Flows;

public class StartFlowTests
{
    [AvaloniaFact]
    public async Task Start_WithoutKey_ShowsApiKeyPage()
    {
        using var shell = new ShellHarness();

        await shell.StartAsync();

        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is ApiKeyView, "страница ключа");
        Assert.Equal(Routes.ApiKey, shell.Navigation.CurrentRoute);
    }

    [AvaloniaFact]
    public async Task SaveValidKey_ReturnsToSessionsAndStoresKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var shell = new ShellHarness(openRouter: StubHttpMessageHandler.Status(HttpStatusCode.OK));
        await shell.StartAsync();
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is ApiKeyView, "страница ключа");
        var model = shell.Provider.GetRequiredService<ApiKeyPageModel>();
        model.ApiKey = "sk-or-test";

        await model.SaveCommand.ExecuteAsync(null);

        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is SessionsView, "список сессий");
        Assert.Equal("sk-or-test", await shell.Secrets.GetAsync(ImageGen.Core.Domain.Keys.SecretNames.OpenRouterApiKey, cancellationToken));
        Assert.Single(shell.OpenRouter.Requests);
        Assert.EndsWith("/key", shell.OpenRouter.Requests[0].RequestUri!.ToString(), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task SaveRejectedKey_StaysOnApiKeyPageWithMessage()
    {
        using var shell = new ShellHarness(openRouter: StubHttpMessageHandler.Status(HttpStatusCode.Unauthorized));
        await shell.StartAsync();
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is ApiKeyView, "страница ключа");
        var model = shell.Provider.GetRequiredService<ApiKeyPageModel>();
        model.ApiKey = "sk-or-bad";

        await model.SaveCommand.ExecuteAsync(null);

        Assert.IsType<ApiKeyView>(shell.Window.PageArea.Content);
        Assert.Equal(AppResources.ApiKeyInvalid, model.StatusText);
    }

    [AvaloniaFact]
    public async Task NewSession_CreatesSessionAndOpensChatRoute()
    {
        using var shell = new ShellHarness();
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        await shell.StartAsync();
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is SessionsView, "список сессий");
        var sessions = shell.Provider.GetRequiredService<SessionsPageModel>();

        await sessions.NewSessionCommand.ExecuteAsync(null);

        Assert.StartsWith(Routes.Chat + "?" + Routes.SessionIdParameter + "=", shell.Navigation.CurrentRoute, StringComparison.Ordinal);
        Assert.Single(await shell.Provider.GetRequiredService<SessionService>().ListAsync(TestContext.Current.CancellationToken));
        Assert.IsType<ChatView>(shell.Window.PageArea.Content);
    }

    [AvaloniaFact]
    public async Task OpenSession_NavigatesToChatRouteOfThatSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var shell = new ShellHarness();
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        var session = await shell.Provider.GetRequiredService<SessionService>().CreateAsync(cancellationToken);
        await shell.StartAsync();
        var sessions = shell.Provider.GetRequiredService<SessionsPageModel>();
        await shell.WaitUntilAsync(() => sessions.Sessions.Count == 1, "строка сессии");

        await sessions.Sessions[0].OpenCommand.ExecuteAsync(null);

        Assert.Equal(Routes.ChatFor(session.Id), shell.Navigation.CurrentRoute);
    }

    [AvaloniaFact]
    public async Task DeleteSession_Confirmed_RemovesRowAndSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ConfirmAsync(default!, default!, default!, default!, default!).ReturnsForAnyArgs(true);
        using var shell = new ShellHarness(dialogs: dialogs);
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        var sessionService = shell.Provider.GetRequiredService<SessionService>();
        await sessionService.CreateAsync(cancellationToken);
        await shell.StartAsync();
        var sessions = shell.Provider.GetRequiredService<SessionsPageModel>();
        await shell.WaitUntilAsync(() => sessions.Sessions.Count == 1, "строка сессии");

        await sessions.Sessions[0].DeleteCommand.ExecuteAsync(null);

        Assert.Empty(sessions.Sessions);
        Assert.Empty(await sessionService.ListAsync(cancellationToken));
    }

    [AvaloniaFact]
    public async Task DeleteSession_Cancelled_KeepsRowAndSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ConfirmAsync(default!, default!, default!, default!, default!).ReturnsForAnyArgs(false);
        using var shell = new ShellHarness(dialogs: dialogs);
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        var sessionService = shell.Provider.GetRequiredService<SessionService>();
        await sessionService.CreateAsync(cancellationToken);
        await shell.StartAsync();
        var sessions = shell.Provider.GetRequiredService<SessionsPageModel>();
        await shell.WaitUntilAsync(() => sessions.Sessions.Count == 1, "строка сессии");

        await sessions.Sessions[0].DeleteCommand.ExecuteAsync(null);

        Assert.Single(sessions.Sessions);
        Assert.Single(await sessionService.ListAsync(cancellationToken));
    }
}

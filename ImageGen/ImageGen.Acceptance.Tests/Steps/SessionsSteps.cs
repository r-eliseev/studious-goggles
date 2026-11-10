using ImageGen.Acceptance.Tests.Support;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Xunit;

namespace ImageGen.Acceptance.Tests.Steps;

[Binding]
public sealed class SessionsSteps(ScenarioContext scenario)
{
    private AppWorld World => scenario.Get<AppWorld>();

    private int SessionsCreated
    {
        get => scenario.TryGetValue("sessions.created", out int count) ? count : 0;
        set => scenario.Set(value, "sessions.created");
    }

    private SessionsPageModel Sessions => World.Provider.GetRequiredService<SessionsPageModel>();

    private ChatPageModel Chat => Assert.IsType<ChatPageModel>(Assert.IsType<ChatView>(World.Window.PageArea.Content).DataContext);

    [When("the user creates a new session")]
    public Task WhenTheUserCreatesANewSession() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await Sessions.NewSessionCommand.ExecuteAsync(null);
        SessionsCreated++;
        var navigation = World.Provider.GetRequiredService<AvaloniaNavigationService>();
        await World.WaitUntilAsync(() => navigation.CurrentRoute?.StartsWith(Routes.Chat + "?", StringComparison.Ordinal) == true,
            "the new session chat to open", cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user returns to the session list")]
    public Task WhenTheUserReturnsToTheSessionList() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.Provider.GetRequiredService<AvaloniaNavigationService>().GoBackAsync();
        await World.WaitUntilAsync(() => Sessions.Sessions.Count == SessionsCreated, "the session rows to load", cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("an empty session chat is shown")]
    public async Task ThenAnEmptySessionChatIsShown()
    {
        await World.WaitUntilAsync(() => Chat.Title.Length > 0, "the new session title to load", TestContext.Current.CancellationToken);
        Assert.Equal(Session.NewSessionTitle, Chat.Title);
        Assert.Empty(Chat.Items);
        Assert.Single(Sessions.Sessions);
        Assert.Equal(Chat.Title, Sessions.Sessions[0].Title);
    }

    [Then("the chat shows the default assistant model")]
    public async Task ThenTheChatShowsTheDefaultAssistantModel()
    {
        await World.WaitUntilAsync(() => Chat.AssistantText.Length > 0, "the assistant model label to load", TestContext.Current.CancellationToken);
        Assert.Contains(SessionSettings.DefaultAssistantModelId, Chat.AssistantText, StringComparison.Ordinal);
        Assert.Contains("1", Chat.ModelsButtonText, StringComparison.Ordinal);
    }

    [Then("one session is in Today and one session is in Earlier")]
    public void ThenOneSessionIsInTodayAndOneSessionIsInEarlier()
    {
        Assert.Single(Sessions.Today);
        Assert.Single(Sessions.Earlier);
    }

}

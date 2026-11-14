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

    private Guid[] SessionOrder
    {
        get => scenario.Get<Guid[]>("sessions.order");
        set => scenario.Set(value, "sessions.order");
    }

    private SessionsPageModel Sessions => World.Provider.GetRequiredService<SessionsPageModel>();

    private ChatPageModel Chat => Assert.IsType<ChatPageModel>(Assert.IsType<ChatView>(World.Window.PageArea.Content).DataContext);

    private SettingsPageModel Settings => Assert.IsType<SettingsPageModel>(Assert.IsType<SettingsView>(World.Window.PageArea.Content).DataContext);

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

    [When("the user chooses the first example from the empty session list")]
    public async Task WhenTheUserChoosesTheFirstExampleFromTheEmptySessionList()
    {
        var text = await HeadlessApp.RunAsync(async _ =>
        {
            var exampleText = Sessions.Examples[0];
            Assert.True(Sessions.HasNoSessions);
            await Sessions.StartExampleCommand.ExecuteAsync(exampleText);
            scenario.Set(exampleText, "sessions.example");
            return exampleText;
        }, TestContext.Current.CancellationToken);

        await World.WaitUntilAsync(() => World.Window.PageArea.Content is ChatView, "the example chat to open", TestContext.Current.CancellationToken);
        await World.WaitUntilAsync(
            () => Chat.Items.OfType<MessageItemViewModel>().Any(message => message.IsUser && message.Text == text),
            "the selected example to be sent",
            TestContext.Current.CancellationToken);
    }

    [When("the user sends the message {string}")]
    public async Task WhenTheUserSendsTheMessage(string message)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Task? execution = null;
        await Task.Run(() => HeadlessApp.RunAsync(_ =>
        {
            Chat.InputText = message;
            execution = Chat.SendCommand.ExecuteAsync(null);
            return Task.FromResult(true);
        }, cancellationToken), cancellationToken).ConfigureAwait(false);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        Assert.NotNull(execution);
        await execution!.WaitAsync(timeout.Token).ConfigureAwait(false);
        Assert.False(Chat.IsBusy);
        Assert.Contains(Chat.Items.OfType<MessageItemViewModel>(), item => item.Text == message);
    }

    [When("the user returns to the session list")]
    public Task WhenTheUserReturnsToTheSessionList() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.Provider.GetRequiredService<AvaloniaNavigationService>().GoBackAsync();
        await World.WaitUntilAsync(() => Sessions.Sessions.Count == SessionsCreated, "the session rows to load", cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the rename prompt will return {string}")]
    public void WhenTheRenamePromptWillReturn(string title) => World.Platform.QueuePrompt(title);

    [When("the user remembers the session order")]
    public void WhenTheUserRemembersTheSessionOrder() => SessionOrder = Sessions.Sessions.Select(item => item.Id).ToArray();

    [When("the user renames the first session")]
    public Task WhenTheUserRenamesTheFirstSession() => HeadlessApp.RunAsync(async _ =>
    {
        await Sessions.Sessions[0].RenameCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user opens the first session")]
    public Task WhenTheUserOpensTheFirstSession() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await Sessions.Sessions[0].OpenCommand.ExecuteAsync(null);
        var navigation = World.Provider.GetRequiredService<AvaloniaNavigationService>();
        await World.WaitUntilAsync(() => navigation.CurrentRoute?.StartsWith(Routes.Chat + "?", StringComparison.Ordinal) == true,
            "the selected session chat to open", cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user selects assistant model {string} as the default")]
    public Task WhenTheUserSelectsAssistantModelAsTheDefault(string modelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var settings = Settings;
        await World.WaitUntilAsync(
            () => settings.AssistantModels.Any(option => option.Reference.ModelId == modelId),
            $"assistant model '{modelId}' to load in Settings",
            cancellationToken);
        settings.SelectedAssistantModel = Assert.Single(settings.AssistantModels, option => option.Reference.ModelId == modelId);
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user saves the default assistant model")]
    public Task WhenTheUserSavesTheDefaultAssistantModel() => HeadlessApp.RunAsync(async _ =>
    {
        await Settings.SaveDefaultAssistantModelCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the delete confirmation is declined")]
    public void WhenTheDeleteConfirmationIsDeclined() => World.Platform.QueueConfirmation(false);

    [When("the delete confirmation is accepted")]
    public void WhenTheDeleteConfirmationIsAccepted() => World.Platform.QueueConfirmation(true);

    [When("the user deletes the first session")]
    public async Task WhenTheUserDeletesTheFirstSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Task? execution = null;
        await Task.Run(() => HeadlessApp.RunAsync(_ =>
        {
            execution = Sessions.Sessions[0].DeleteCommand.ExecuteAsync(null);
            return Task.FromResult(true);
        }, cancellationToken), cancellationToken).ConfigureAwait(false);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        Assert.NotNull(execution);
        await execution!.WaitAsync(timeout.Token).ConfigureAwait(false);
    }

    [Then("the session order is unchanged")]
    public void ThenTheSessionOrderIsUnchanged() =>
        Assert.Equal(SessionOrder, Sessions.Sessions.Select(item => item.Id));

    [Then("the chat title is {string}")]
    public void ThenTheChatTitleIs(string title) => Assert.Equal(title, Chat.Title);

    [Then("the first session retains its original title")]
    public void ThenTheFirstSessionRetainsItsOriginalTitle() =>
        Assert.Equal(Session.NewSessionTitle, Sessions.Sessions[0].Title);

    [Then("the first session row shows {string}")]
    public async Task ThenTheFirstSessionRowShows(string text)
    {
        await World.WaitUntilAsync(
            () => Sessions.Sessions.Count > 0 && Sessions.Sessions[0].DetailsText.EndsWith(text, StringComparison.Ordinal),
            $"the first session row to show '{text}'",
            TestContext.Current.CancellationToken);
        Assert.EndsWith(text, Sessions.Sessions[0].DetailsText, StringComparison.Ordinal);
    }

    [Then("the selected example is sent once")]
    public async Task ThenTheSelectedExampleIsSentOnce()
    {
        var text = scenario.Get<string>("sessions.example");
        await World.WaitUntilAsync(
            () => Chat.Items.OfType<MessageItemViewModel>().Any(message => message.IsUser && message.Text == text),
            "the selected example message to appear",
            TestContext.Current.CancellationToken);
        var userMessages = Chat.Items.OfType<MessageItemViewModel>().Where(message => message.IsUser).ToArray();
        Assert.Equal(text, Assert.Single(userMessages).Text);
        Assert.Equal(text[..Math.Min(40, text.Length)], Chat.Title);
    }

    [Then("the app shows a rename error")]
    public void ThenTheAppShowsARenameError() => Assert.NotEmpty(World.Platform.Alerts);

    [Then("the first session remains in the list")]
    public void ThenTheFirstSessionRemainsInTheList() => Assert.Single(Sessions.Sessions);

    [Then("no sessions are in the list")]
    public void ThenNoSessionsAreInTheList() => Assert.Empty(Sessions.Sessions);

    [Then("an empty session chat is shown")]
    public async Task ThenAnEmptySessionChatIsShown()
    {
        await World.WaitUntilAsync(() => Chat.Title.Length > 0, "the new session title to load", TestContext.Current.CancellationToken);
        Assert.Equal(Session.NewSessionTitle, Chat.Title);
        Assert.Empty(Chat.Items);
        Assert.Single(Sessions.Sessions);
        Assert.Equal(Chat.Title, Sessions.Sessions[0].Title);
    }

    [Then("the session title is the first {int} characters of the message")]
    public void ThenTheSessionTitleIsTheFirstCharactersOfTheMessage(int length)
    {
        var message = Assert.IsType<MessageItemViewModel>(Chat.Items.OfType<MessageItemViewModel>().First()).Text;
        Assert.Equal(message[..length], Chat.Title);
    }

    [Then("the chat shows the default assistant model")]
    public async Task ThenTheChatShowsTheDefaultAssistantModel()
    {
        await World.WaitUntilAsync(() => Chat.AssistantText.Length > 0, "the assistant model label to load", TestContext.Current.CancellationToken);
        Assert.Contains(SessionSettings.DefaultAssistantModelId, Chat.AssistantText, StringComparison.Ordinal);
        Assert.Contains("1", Chat.ModelsButtonText, StringComparison.Ordinal);
    }

    [Then("the chat shows assistant model {string}")]
    public async Task ThenTheChatShowsAssistantModel(string modelId)
    {
        await World.WaitUntilAsync(
            () => Chat.AssistantText.Contains(modelId, StringComparison.Ordinal),
            $"assistant model '{modelId}' to appear in the chat",
            TestContext.Current.CancellationToken);
        Assert.Contains(modelId, Chat.AssistantText, StringComparison.Ordinal);
    }

    [Then("one session is in Today and one session is in Earlier")]
    public void ThenOneSessionIsInTodayAndOneSessionIsInEarlier()
    {
        Assert.Single(Sessions.Today);
        Assert.Single(Sessions.Earlier);
    }

}

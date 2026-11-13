using System.Text.Json;
using ImageGen.Acceptance.Tests.Support;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Services;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
using Reqnroll;
using Xunit;

namespace ImageGen.Acceptance.Tests.Steps;

[Binding]
public sealed class ChatSteps(ScenarioContext scenario)
{
    private AppWorld World => scenario.Get<AppWorld>();

    private ChatPageModel Chat => Assert.IsType<ChatPageModel>(Assert.IsType<ChatView>(scenario.Get<AppWorld>().Window.PageArea.Content).DataContext);

    [Then("the assistant message {string} is shown")]
    public void ThenTheAssistantMessageIsShown(string text) =>
        Assert.Contains(Chat.Items.OfType<MessageItemViewModel>(), message => message.Text == text);

    [Then("the user message {string} remains in the chat")]
    public void ThenTheUserMessageRemainsInTheChat(string text) =>
        Assert.Contains(Chat.Items.OfType<MessageItemViewModel>(), message => message.IsUser && message.Text == text);

    [Then("an assistant error message {string} is shown")]
    public void ThenAnAssistantErrorMessageIsShown(string text) =>
        Assert.Contains(Chat.Items.OfType<MessageItemViewModel>(), message => message.IsError && !message.IsUser && message.Text == text);

    [When("the user retries the assistant response")]
    public Task WhenTheUserRetriesTheAssistantResponse() => HeadlessApp.RunAsync(async _ =>
    {
        var error = Assert.Single(Chat.Items.OfType<MessageItemViewModel>(), message => message.IsError && message.CanRetry);
        await error.RetryCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("no run is shown in the chat")]
    public void ThenNoRunIsShownInTheChat() =>
        Assert.DoesNotContain(Chat.Items, item => item is RunItemViewModel);

    [When("the user enters {string} in the chat input")]
    public Task WhenTheUserEntersInTheChatInput(string text) => HeadlessApp.RunAsync(_ =>
    {
        Chat.InputText = text;
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [When("the user attaches the sample PNG")]
    public Task WhenTheUserAttachesTheSamplePng() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var imagePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../Assets/reference.png"));
        var content = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        var image = new PickedImage(content, "image/png", "reference.png");
        World.Platform.QueuePickedImage(image);
        await Chat.AttachImageCommand.ExecuteAsync(null);
        Assert.Single(Chat.PendingAttachments);
        scenario.Set($"data:image/png;base64,{Convert.ToBase64String(content)}", "chat.reference-image-data-url");
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user submits the chat message {string}")]
    public async Task WhenTheUserSubmitsTheChatMessage(string message)
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
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        Assert.NotNull(execution);
        try
        {
            await execution!.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            var requests = World.Network.Requests.Select(request => $"{request.Method} {request.Address}").ToArray();
            var isBusy = await HeadlessApp.RunAsync(_ => Task.FromResult(Chat.IsBusy), CancellationToken.None);
            throw new TimeoutException($"Sending the chat message timed out; Chat.IsBusy={isBusy}; requests={string.Join(", ", requests)}.");
        }

        await HeadlessApp.RunAsync(_ =>
        {
            Assert.False(Chat.IsBusy);
            Assert.Contains(Chat.Items.OfType<MessageItemViewModel>(), item => item.IsUser && item.Text == message);
            return Task.FromResult(true);
        }, cancellationToken);
    }

    [Then("sending a chat message is unavailable")]
    public void ThenSendingAChatMessageIsUnavailable() => Assert.False(Chat.SendCommand.CanExecute(null));

    [Then("the assistant tool prompt {string} is preserved in the run, card, and generation request")]
    public Task ThenTheAssistantToolPromptIsPreserved(string prompt) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.WaitUntilAsync(
            () => Chat.Items.OfType<RunItemViewModel>().Any(),
            "the generation run to appear in the chat",
            cancellationToken);

        var run = Assert.Single(Chat.Items.OfType<RunItemViewModel>());
        Assert.Contains(prompt, run.PromptText, StringComparison.Ordinal);
        var card = Assert.Single(run.Generations);
        Assert.Equal(prompt, card.Prompt);

        await World.WaitUntilAsync(
            () => World.Network.Requests.Any(request =>
                request.Method == "POST" && new Uri(request.Address).AbsolutePath == "/api/v1/images"),
            "the OpenRouter image request to be recorded",
            cancellationToken);
        var imageRequest = Assert.Single(World.Network.Requests, request =>
            request.Method == "POST" && new Uri(request.Address).AbsolutePath == "/api/v1/images");

        using var body = JsonDocument.Parse(imageRequest.Body!);
        Assert.Equal(prompt, body.RootElement.GetProperty("prompt").GetString());
        Assert.Equal(card.ModelId, body.RootElement.GetProperty("model").GetString());
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the latest assistant request contains the system prompt and current run prompt {string}")]
    public Task ThenTheLatestAssistantRequestContainsTheSystemPromptAndCurrentRunPrompt(string prompt) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        bool HasTwoAssistantRequests() => World.Network.Requests.Count(request =>
            request.Method == "POST" && new Uri(request.Address).AbsolutePath == "/api/v1/chat/completions") >= 2;

        await World.WaitUntilAsync(HasTwoAssistantRequests, "the second assistant request to be recorded", cancellationToken);
        var request = World.Network.Requests.Last(recorded =>
            recorded.Method == "POST" && new Uri(recorded.Address).AbsolutePath == "/api/v1/chat/completions");
        using var body = JsonDocument.Parse(request.Body!);
        var messages = body.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Contains(messages, message =>
            message.GetProperty("role").GetString() == "system"
            && message.GetProperty("content").GetString() == AssistantPrompts.System);
        Assert.Contains(messages, message =>
            message.GetProperty("role").GetString() == "system"
            && message.GetProperty("content").GetString() == AssistantPrompts.CurrentPrompt(prompt));
        return true;
    }, TestContext.Current.CancellationToken);
}

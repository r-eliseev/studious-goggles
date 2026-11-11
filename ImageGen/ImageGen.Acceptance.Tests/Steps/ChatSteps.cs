using System.Text.Json;
using ImageGen.Acceptance.Tests.Support;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Services;
using ImageGen.Presentation.PageModels;
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

    [Then("no run is shown in the chat")]
    public void ThenNoRunIsShownInTheChat() =>
        Assert.DoesNotContain(Chat.Items, item => item is RunItemViewModel);

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

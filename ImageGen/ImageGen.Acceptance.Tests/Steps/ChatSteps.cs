using ImageGen.Acceptance.Tests.Support;
using ImageGen.Avalonia.Views;
using ImageGen.Presentation.PageModels;
using Reqnroll;
using Xunit;

namespace ImageGen.Acceptance.Tests.Steps;

[Binding]
public sealed class ChatSteps(ScenarioContext scenario)
{
    private ChatPageModel Chat => Assert.IsType<ChatPageModel>(Assert.IsType<ChatView>(scenario.Get<AppWorld>().Window.PageArea.Content).DataContext);

    [Then("the assistant message {string} is shown")]
    public void ThenTheAssistantMessageIsShown(string text) =>
        Assert.Contains(Chat.Items.OfType<MessageItemViewModel>(), message => message.Text == text);

    [Then("no run is shown in the chat")]
    public void ThenNoRunIsShownInTheChat() =>
        Assert.DoesNotContain(Chat.Items, item => item is RunItemViewModel);
}

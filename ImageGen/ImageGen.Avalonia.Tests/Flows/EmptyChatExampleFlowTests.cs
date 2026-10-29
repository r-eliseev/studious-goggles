using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Avalonia.Tests.Flows;

public class EmptyChatExampleFlowTests
{
    [AvaloniaFact]
    public async Task PressingAnExample_OpensNewChatAndSendsItOnce()
    {
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create());
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        shell.Window.Width = 1100;
        shell.Window.Show();
        await shell.Navigation.GoToAsync(Routes.Sessions);
        shell.Window.UpdateLayout();

        var view = Assert.IsType<SessionsView>(shell.Window.PageArea.Content);
        var example = view.GetVisualDescendants().OfType<Button>()
            .Where(button => button.IsEffectivelyVisible && AutomationProperties.GetName(button) == AppResources.EmptyChatExample1)
            .Single();
        example.Command!.Execute(example.CommandParameter);

        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is ChatView, "чат открыт");
        var chat = (ChatPageModel)((Control)shell.Window.PageArea.Content!).DataContext!;
        await shell.WaitUntilAsync(() => chat.Items.OfType<MessageItemViewModel>().Any(message => message.IsUser), "пример отправлен");
        var sent = chat.Items.OfType<MessageItemViewModel>().Single(message => message.IsUser);
        Assert.Equal(AppResources.EmptyChatExample1, sent.Text);
        Assert.Single(chat.Items.OfType<MessageItemViewModel>(), message => message.IsUser);
    }
}

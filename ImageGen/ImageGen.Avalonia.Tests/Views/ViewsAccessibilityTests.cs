using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;

namespace ImageGen.Avalonia.Tests.Views;

public class ViewsAccessibilityTests
{
    [AvaloniaFact]
    public async Task Views_InteractiveControls_HaveAutomationName()
    {
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create());
        // Narrow window: the Models button navigates to the screen (wide opens a pane, task 071).
        shell.Window.Width = 600;
        shell.Window.Show();

        var chat = await shell.OpenNewChatAsync();
        AssertNamed(shell, "чат");

        await chat.OpenModelsCommand.ExecuteAsync(null);
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is SessionModelsView, "экран моделей");
        AssertNamed(shell, "модели");

        await shell.Navigation.GoToAsync(Routes.ApiKeyFor("openrouter"));
        AssertNamed(shell, "ключ");

        await shell.Navigation.GoToAsync(Routes.Sessions);
        AssertNamed(shell, "сессии");

        await shell.Navigation.GoToAsync(Routes.Settings);
        AssertNamed(shell, "настройки");
    }

    private static void AssertNamed(ShellHarness shell, string page)
    {
        // Templates of the new page are applied by a layout pass; without it the visual tree is still empty.
        shell.Window.UpdateLayout();
        var view = Assert.IsAssignableFrom<Control>(shell.Window.PageArea.Content);
        var controls = view.GetVisualDescendants().OfType<Control>()
            // Controls created by a template (the spin buttons of NumericUpDown, for example) are not ours to name.
            .Where(control => control.TemplatedParent is null && control is Button or TextBox or CheckBox or NumericUpDown)
            .ToList();

        Assert.True(controls.Count > 0, page + ": нет интерактивных контролов в визуальном дереве (" + view.GetType().Name + ")");
        foreach (var control in controls)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)),
                $"{page}: {control.GetType().Name} has no AutomationProperties.Name");
        }
    }
}

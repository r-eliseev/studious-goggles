using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Presentation.PageModels;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Tests.Views;

public class ViewsAccessibilityTests
{
    [AvaloniaFact]
    public void Views_InteractiveControls_HaveAutomationName()
    {
        using var shell = new ShellHarness();
        var window = shell.Window;
        window.Show();
        var views = new Control[]
        {
            new SessionsView { DataContext = shell.Provider.GetRequiredService<SessionsPageModel>() },
            new ApiKeyView { DataContext = shell.Provider.GetRequiredService<ApiKeyPageModel>() },
        };

        foreach (var view in views)
        {
            window.PageArea.Content = view;
            Dispatcher.UIThread.RunJobs();
            var controls = view.GetVisualDescendants().OfType<Control>()
                .Where(control => control is Button or TextBox)
                .ToList();

            Assert.NotEmpty(controls);
            foreach (var control in controls)
            {
                Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)), $"{control.GetType().Name} has no AutomationProperties.Name");
            }
        }
    }
}

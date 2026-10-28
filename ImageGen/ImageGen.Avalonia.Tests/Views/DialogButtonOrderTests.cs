using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ImageGen.Avalonia.Views;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Avalonia.Tests.Views;

public class DialogButtonOrderTests
{
    [AvaloniaFact]
    public void ModelsView_SaveIsFirstOnThisPlatform()
    {
        var view = new SessionModelsView();

        var row = view.FindControl<Panel>("ButtonRow")!;

        Assert.Equal(OperatingSystem.IsMacOS() ? AppResources.Cancel : AppResources.Save, AutomationProperties.GetName(row.Children[0]));
    }

    [AvaloniaFact]
    public void ParametersView_SaveIsFirstOnThisPlatform()
    {
        var view = new SessionParametersView();

        var row = view.FindControl<Panel>("ButtonRow")!;

        Assert.Equal(OperatingSystem.IsMacOS() ? AppResources.Cancel : AppResources.Save, AutomationProperties.GetName(row.Children[0]));
    }
}

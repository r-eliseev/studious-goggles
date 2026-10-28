using Avalonia.Headless.XUnit;
using ImageGen.Avalonia.Views;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Avalonia.Tests.Views;

public class MainWindowTests
{
    [AvaloniaFact]
    public void Show_Window_HasAppTitleAndIsVisible()
    {
        var window = new MainWindow();

        window.Show();

        Assert.True(window.IsVisible);
        Assert.Equal(AppResources.AppTitle, window.Title);
    }
}

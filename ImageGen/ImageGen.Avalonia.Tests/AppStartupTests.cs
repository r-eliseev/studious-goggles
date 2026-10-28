using Avalonia;
using Avalonia.Headless.XUnit;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Avalonia.Tests;

public class AppStartupTests
{
    [AvaloniaFact]
    public void Startup_BuildsContainerAndMainWindow()
    {
        var app = Assert.IsType<App>(Application.Current);

        Assert.NotNull(app.Services);
        Assert.NotNull(app.Services.GetService(typeof(SessionsPageModel)));
        Assert.NotNull(app.MainWindow);
        Assert.Equal(AppResources.AppTitle, app.MainWindow.Title);
    }
}

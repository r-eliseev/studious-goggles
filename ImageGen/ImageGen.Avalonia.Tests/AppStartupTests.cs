using Avalonia;
using Avalonia.Headless.XUnit;
using ImageGen.Avalonia.Views;
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
        // Startup shows the sessions page; with no key saved it moves on to the key page at once.
        var content = app.MainWindow.PageArea.Content;
        Assert.True(content is SessionsView or ApiKeyView, "root page: " + content?.GetType().Name);
    }
}

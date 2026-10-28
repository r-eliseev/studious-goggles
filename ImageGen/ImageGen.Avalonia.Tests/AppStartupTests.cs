using Avalonia;
using Avalonia.Headless.XUnit;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Platform;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;

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

    [AvaloniaFact]
    public void Startup_UsesTemporaryDataFolderAndInMemoryKeys()
    {
        var app = Assert.IsType<App>(Application.Current);
        var overrides = App.HostOverrides;

        Assert.NotNull(overrides);
        Assert.Equal(overrides.Paths.DataDirectory, app.Services!.GetRequiredService<IAppPaths>().DataDirectory);
        Assert.Contains("imagegen-avalonia-", overrides.Paths.DataDirectory, StringComparison.Ordinal);
        Assert.False(overrides.Secrets.IsPersistentAsync(TestContext.Current.CancellationToken).Result);
    }
}

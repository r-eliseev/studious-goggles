using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using ImageGen.Acceptance.Tests.Support;
using ImageGen.Avalonia.Services;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Presentation;
using ImageGen.Presentation.Appearance;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Reqnroll;
using Xunit;

namespace ImageGen.Acceptance.Tests.Steps;

[Binding]
public sealed class SettingsSteps(ScenarioContext scenario)
{
    private AppWorld World => scenario.Get<AppWorld>();

    private SettingsPageModel Settings => World.Provider.GetRequiredService<SettingsPageModel>();

    [Then("the saved appearance is the default")]
    public async Task ThenTheSavedAppearanceIsTheDefault()
    {
        var appearance = World.Provider.GetRequiredService<AppearanceService>();
        Assert.Equal(AppearanceSettings.Default, appearance.Current);
        Assert.False(File.Exists(Path.Combine(World.DataDirectory, AppearanceSettingsFile.FileName)));
    }

    [When("the user chooses the Gallery style and dark theme")]
    public Task WhenTheUserChoosesTheGalleryStyleAndDarkTheme() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var model = Settings;
        await model.LoadCommand.ExecuteAsync(null).WaitAsync(cancellationToken);
        var gallery = Assert.Single(model.Styles, option => option.Style == AppearanceStyle.Gallery);
        await gallery.SelectCommand.ExecuteAsync(null);
        model.SelectThemeCommand.Execute(AppearanceTheme.Dark);
        await World.WaitUntilAsync(() => World.Provider.GetRequiredService<AppearanceService>().Current == new AppearanceSettings(AppearanceStyle.Gallery, AppearanceTheme.Dark), "the chosen appearance to be saved", cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the active appearance is Gallery with the dark theme")]
    public Task ThenTheActiveAppearanceIsGalleryWithTheDarkTheme() => HeadlessApp.RunAsync(_ =>
    {
        Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
        var include = Assert.Single(Application.Current!.Resources.MergedDictionaries.OfType<ResourceInclude>(), item => item.Source?.AbsoluteUri.EndsWith("/Gallery.axaml", StringComparison.Ordinal) == true);
        Assert.Equal(AvaloniaAppearanceApplier.ThemeUri(AppearanceStyle.Gallery), include.Source!.AbsoluteUri);
        Assert.True(Application.Current.TryFindResource("IgAccentColor", ThemeVariant.Dark, out var accent));
        Assert.NotNull(accent);
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [When("the user opens settings from the session list")]
    public Task WhenTheUserOpensSettingsFromTheSessionList() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var sessions = World.Provider.GetRequiredService<SessionsPageModel>();
        await sessions.OpenSettingsCommand.ExecuteAsync(null);
        var navigation = World.Provider.GetRequiredService<AvaloniaNavigationService>();
        await World.WaitUntilAsync(() => navigation.CurrentRoute == Routes.Settings, "the Settings route", cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the Settings route is open")]
    public Task ThenTheSettingsRouteIsOpen() => HeadlessApp.RunAsync(_ =>
    {
        Assert.Equal(Routes.Settings, World.Provider.GetRequiredService<AvaloniaNavigationService>().CurrentRoute);
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [Given("an old log file exists")]
    public async Task GivenAnOldLogFileExists()
    {
        var directory = Path.Combine(World.DataDirectory, "logs");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "imagegen-20000101.log"), "old log", TestContext.Current.CancellationToken);
    }

    [When("log clearing is confirmed")]
    public void WhenLogClearingIsConfirmed() => World.Platform.QueueConfirmation(true);

    [When("the user clears logs")]
    public Task WhenTheUserClearsLogs() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var model = Settings;
        if (model.Styles.Count == 0)
        {
            await model.LoadCommand.ExecuteAsync(null);
        }

        model.ClearLogsCommand.Execute(null);
        await World.WaitUntilAsync(() => Directory.GetFiles(Path.Combine(World.DataDirectory, "logs"), "imagegen-20000101.log").Length == 0,
            "old log files to be deleted", cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("old log files are deleted")]
    public void ThenOldLogFilesAreDeleted() => Assert.False(File.Exists(Path.Combine(World.DataDirectory, "logs", "imagegen-20000101.log")));

    [Then("the app writes another log entry")]
    public async Task ThenTheAppWritesAnotherLogEntry()
    {
        var path = Path.Combine(World.DataDirectory, "logs", $"imagegen-{DateTime.UtcNow:yyyyMMdd}.log");
        var logger = World.Provider.GetRequiredService<ILogger<SettingsSteps>>();
        logger.LogInformation("Acceptance log continuation marker.");
        await World.WaitUntilAsync(() => File.Exists(path)
            && File.ReadAllText(path).Contains("Acceptance log continuation marker.", StringComparison.Ordinal),
            "the log file to receive another entry", TestContext.Current.CancellationToken);
    }
}

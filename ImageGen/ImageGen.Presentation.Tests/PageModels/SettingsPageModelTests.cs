using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Platform;
using ImageGen.Presentation;
using ImageGen.Presentation.Appearance;
using ImageGen.Presentation.Logging;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ImageGen.Presentation.Tests.PageModels;

public sealed class SettingsPageModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "imagegen-tests", Guid.NewGuid().ToString("N"));
    private readonly ISecretStore _secrets = Substitute.For<ISecretStore>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IFolderLauncher _folders = Substitute.For<IFolderLauncher>();
    private readonly AppearanceService _appearance;

    public SettingsPageModelTests()
    {
        Directory.CreateDirectory(_directory);
        _appearance = new AppearanceService(new TestPaths(_directory), NullLogger<AppearanceService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task Load_KeySaved_ShowsLastFourCharacters()
    {
        _secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns("sk-or-v1-abcd1234");
        var model = CreateModel();

        await model.LoadCommand.ExecuteAsync(null);

        Assert.Equal(string.Format(AppResources.SettingsKeyConnectedFormat, "1234"), model.KeyText);
    }

    [Fact]
    public async Task Load_NoKey_ShowsNotConnected()
    {
        _secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns((string?)null);
        var model = CreateModel();

        await model.LoadCommand.ExecuteAsync(null);

        Assert.Equal(AppResources.SettingsKeyMissing, model.KeyText);
    }

    [Fact]
    public async Task Load_ShowsCurrentStyleSelected()
    {
        await _appearance.SaveAsync(new AppearanceSettings(AppearanceStyle.Notebook, AppearanceTheme.System), CancellationToken.None);
        var model = CreateModel();

        await model.LoadCommand.ExecuteAsync(null);

        Assert.Single(model.Styles, s => s.IsSelected);
        Assert.True(model.Styles.Single(s => s.Style == AppearanceStyle.Notebook).IsSelected);
    }

    [Fact]
    public async Task SelectStyle_Gallery_SavesAndMarksSelected()
    {
        var model = CreateModel();
        await model.LoadCommand.ExecuteAsync(null);

        await model.Styles.Single(s => s.Style == AppearanceStyle.Gallery).SelectCommand.ExecuteAsync(null);

        Assert.Equal(AppearanceStyle.Gallery, _appearance.Current.Style);
        Assert.True(model.Styles.Single(s => s.Style == AppearanceStyle.Gallery).IsSelected);
        Assert.False(model.Styles.Single(s => s.Style == AppearanceStyle.Quiet).IsSelected);
    }

    [Fact]
    public async Task SelectTheme_Dark_Saves()
    {
        var model = CreateModel();
        await model.LoadCommand.ExecuteAsync(null);

        await model.SelectThemeCommand.ExecuteAsync(AppearanceTheme.Dark);

        Assert.Equal(AppearanceTheme.Dark, _appearance.Current.Theme);
        Assert.Equal(AppearanceTheme.Dark, model.SelectedTheme);
    }

    [Fact]
    public async Task ChangeKey_NavigatesToApiKey()
    {
        var model = CreateModel();

        await model.ChangeKeyCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(Routes.ApiKey);
    }

    [Fact]
    public async Task OpenLogs_CallsLauncherWithLogFolder()
    {
        _folders.IsSupported.Returns(true);
        var model = CreateModel();

        await model.OpenLogsCommand.ExecuteAsync(null);

        await _folders.Received(1).OpenAsync(ImageGenLogging.LogDirectoryFor(_directory), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CanOpenLogs_FollowsLauncher()
    {
        _folders.IsSupported.Returns(false);
        Assert.False(CreateModel().CanOpenLogs);

        _folders.IsSupported.Returns(true);
        Assert.True(CreateModel().CanOpenLogs);
    }

    private SettingsPageModel CreateModel() => new(_appearance, _secrets, _navigation, _folders, new TestPaths(_directory));

    private sealed record TestPaths(string DataDirectory) : IAppPaths;
}

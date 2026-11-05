using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Search;
using ImageGen.Core.Services;
using ImageGen.Presentation;
using ImageGen.Presentation.Appearance;
using ImageGen.Presentation.Logging;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ImageGen.Presentation.Tests.PageModels;

public sealed class SettingsPageModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "imagegen-tests", Guid.NewGuid().ToString("N"));
    private readonly ISecretStore _secrets = Substitute.For<ISecretStore>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IFolderLauncher _folders = Substitute.For<IFolderLauncher>();
    private readonly ILogSharer _logSharer = Substitute.For<ILogSharer>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IProviderRouter _router = Substitute.For<IProviderRouter>();
    private readonly IDefaultAssistantModel _defaultAssistantModel = DefaultAssistantModels.Create();
    private readonly ISemanticIndexController _semanticIndex = Substitute.For<ISemanticIndexController>();
    private readonly IMainThread _mainThread = Substitute.For<IMainThread>();
    private readonly ProviderRegistry _registry = new([
        new ProviderConfig(ProviderIds.OpenRouter, ProviderKind.OpenRouter, "OpenRouter", new Uri("https://openrouter.ai/api/v1/"), TimeSpan.FromSeconds(10), []),
        new ProviderConfig(ProviderIds.Fal, ProviderKind.Fal, "fal.ai", new Uri("https://queue.fal.run/"), TimeSpan.FromSeconds(10), []),
        new ProviderConfig("together", ProviderKind.OpenAiCompatible, "Together AI", new Uri("https://api.together.ai/v1/"), TimeSpan.FromSeconds(10), []),
    ]);
    private readonly ApiKeyService _keyService;
    private readonly AppearanceService _appearance;

    public SettingsPageModelTests()
    {
        Directory.CreateDirectory(_directory);
        _keyService = new ApiKeyService(_router, _secrets, NullLogger<ApiKeyService>.Instance);
        _appearance = new AppearanceService(new TestPaths(_directory), NullLogger<AppearanceService>.Instance);
        _semanticIndex.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(new SemanticIndexStatus(false, 0, 0, SemanticIndexState.Off));
        _semanticIndex.GetSourceCountAsync(Arg.Any<CancellationToken>()).Returns(0);
        _mainThread.When(thread => thread.BeginInvokeOnMainThread(Arg.Any<Action>())).Do(call => call.Arg<Action>().Invoke());
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

        var row = Assert.Single(model.ProviderKeys, item => item.ProviderId == ProviderIds.OpenRouter);
        Assert.Equal(string.Format(AppResources.SettingsKeyConnectedFormat, "1234"), row.KeyText);
        Assert.True(row.HasKey);
    }

    [Fact]
    public async Task Load_NoKey_ShowsNotConnected()
    {
        _secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns((string?)null);
        var model = CreateModel();

        await model.LoadCommand.ExecuteAsync(null);

        var row = Assert.Single(model.ProviderKeys, item => item.ProviderId == ProviderIds.OpenRouter);
        Assert.Equal(AppResources.SettingsKeyMissing, row.KeyText);
        Assert.False(row.HasKey);
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
    public async Task Load_ListsConfiguredProvidersInOrderAndRefreshesWithoutDuplicates()
    {
        _secrets.GetAsync("provider.fal.api_key", Arg.Any<CancellationToken>()).Returns("fal-5678");
        _secrets.GetAsync("provider.together.api_key", Arg.Any<CancellationToken>()).Returns((string?)null);
        var model = CreateModel();

        await model.LoadCommand.ExecuteAsync(null);
        var rows = model.ProviderKeys.ToArray();
        await model.LoadCommand.ExecuteAsync(null);

        Assert.Equal([ProviderIds.OpenRouter, ProviderIds.Fal, "together"], model.ProviderKeys.Select(row => row.ProviderId));
        Assert.Equal(rows, model.ProviderKeys);
        Assert.Equal("fal.ai", model.ProviderKeys[1].DisplayName);
        Assert.Equal(string.Format(AppResources.SettingsKeyConnectedFormat, "5678"), model.ProviderKeys[1].KeyText);
    }

    [Fact]
    public async Task SaveDefaultAssistantModel_SelectedModel_PersistsReference()
    {
        var model = CreateModel();
        model.SelectedAssistantModel = new AssistantModelOption(new ModelRef("claude", "vendor/assistant"), "Assistant", "Claude API");

        await model.SaveDefaultAssistantModelCommand.ExecuteAsync(null);

        await _defaultAssistantModel.Received(1).SaveAsync(new ModelRef("claude", "vendor/assistant"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleSemanticSearch_ConfirmsTextCountBeforeEnabling()
    {
        _semanticIndex.GetSourceCountAsync(Arg.Any<CancellationToken>()).Returns(13);
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var model = CreateModel();

        await model.ToggleSemanticSearchCommand.ExecuteAsync(null);

        await _semanticIndex.Received(1).EnableAsync(Arg.Any<CancellationToken>());
        await _dialogs.Received(1).ConfirmAsync(
            AppResources.SemanticSearchTitle,
            Arg.Is<string>(message => message.Contains("13 of 13", StringComparison.Ordinal) && message.Contains("paid", StringComparison.Ordinal)),
            AppResources.SemanticSearchEnable,
            AppResources.Cancel);
    }

    [Fact]
    public async Task ProviderChangeCommand_NavigatesWithProviderId()
    {
        var model = CreateModel();
        await model.LoadCommand.ExecuteAsync(null);

        await model.ProviderKeys.Single(item => item.ProviderId == "together").ChangeCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(Routes.ApiKeyFor("together"));
    }

    [Fact]
    public async Task RemoveCommand_Cancel_DoesNotRemoveSecret()
    {
        _secrets.GetAsync("provider.together.api_key", Arg.Any<CancellationToken>()).Returns("secret-5678");
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var model = CreateModel();
        await model.LoadCommand.ExecuteAsync(null);
        var row = model.ProviderKeys.Single(item => item.ProviderId == "together");

        await row.RemoveCommand.ExecuteAsync(null);

        await _secrets.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.True(row.HasKey);
    }

    [Theory]
    [InlineData("together", "provider.together.api_key", false)]
    [InlineData(ProviderIds.OpenRouter, SecretNames.OpenRouterApiKey, true)]
    public async Task RemoveCommand_Confirm_RemovesOnlySelectedSecret(string providerId, string secretName, bool openRouter)
    {
        var stored = true;
        _secrets.GetAsync(secretName, Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(stored ? "secret-5678" : null));
        _secrets.RemoveAsync(secretName, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            stored = false;
            return Task.CompletedTask;
        });
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var model = CreateModel();
        await model.LoadCommand.ExecuteAsync(null);
        var row = model.ProviderKeys.Single(item => item.ProviderId == providerId);

        await row.RemoveCommand.ExecuteAsync(null);

        await _secrets.Received(1).RemoveAsync(secretName, Arg.Any<CancellationToken>());
        await _secrets.DidNotReceive().RemoveAsync(Arg.Is<string>(name => name != secretName), Arg.Any<CancellationToken>());
        Assert.False(row.HasKey);
        Assert.Equal(AppResources.SettingsKeyMissing, row.KeyText);
        await _navigation.DidNotReceive().GoBackAsync();
        if (openRouter)
        {
            await _dialogs.Received(1).ConfirmAsync(
                AppResources.ProviderKeyRemoveTitle,
                Arg.Is<string>(message => message.Contains(AppResources.ProviderKeyRemoveOpenRouterWarning, StringComparison.Ordinal)),
                AppResources.ProviderKeyRemove,
                AppResources.Cancel);
        }
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
    public void CanShareLogs_FollowsSharer()
    {
        _logSharer.IsSupported.Returns(true);
        Assert.True(CreateModel().CanShareLogs);

        _logSharer.IsSupported.Returns(false);
        Assert.False(CreateModel().CanShareLogs);
    }

    [Fact]
    public async Task ShareLogs_NoFiles_ShowsMessageWithoutOpeningShareSheet()
    {
        var model = CreateModel();

        await model.ShareLogsCommand.ExecuteAsync(null);

        await _dialogs.Received(1).AlertAsync(AppResources.SettingsLogs, AppResources.SettingsNoLogs);
        await _logSharer.DidNotReceive().ShareAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShareLogs_WithFiles_SharesCopiesOutsideLogFolder()
    {
        var logDirectory = ImageGenLogging.LogDirectoryFor(_directory);
        Directory.CreateDirectory(logDirectory);
        File.WriteAllText(Path.Combine(logDirectory, "imagegen-20261101.log"), "older");
        File.WriteAllText(Path.Combine(logDirectory, "imagegen-20261102.log"), "current");
        var model = CreateModel();

        await model.ShareLogsCommand.ExecuteAsync(null);

        await _logSharer.Received(1).ShareAsync(Arg.Is<IReadOnlyList<string>>(files =>
            files.Count == 2
            && files.All(file => !file.StartsWith(logDirectory, StringComparison.Ordinal))
            && files.Select(Path.GetFileName).SequenceEqual(new[] { "imagegen-20261101.log", "imagegen-20261102.log" })), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearLogs_Cancel_KeepsFiles()
    {
        var paths = CreateLogFiles("imagegen-20261101.log", "imagegen-20261102.log");
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns(false);
        var model = CreateModel();

        await model.ClearLogsCommand.ExecuteAsync(null);

        Assert.All(paths, path => Assert.True(File.Exists(path)));
        await _dialogs.DidNotReceive().AlertAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ClearLogs_Confirmed_DeletesOlderAndKeepsNewest()
    {
        var paths = CreateLogFiles("imagegen-20261101.log", "imagegen-20261102.log");
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns(true);
        var model = CreateModel();

        await model.ClearLogsCommand.ExecuteAsync(null);

        Assert.False(File.Exists(paths[0]));
        Assert.True(File.Exists(paths[1]));
        await _dialogs.Received(1).AlertAsync(AppResources.SettingsLogs, Arg.Is<string>(message => message.Contains("1", StringComparison.Ordinal)));
    }

    [Fact]
    public void CanOpenLogs_FollowsLauncher()
    {
        _folders.IsSupported.Returns(false);
        Assert.False(CreateModel().CanOpenLogs);

        _folders.IsSupported.Returns(true);
        Assert.True(CreateModel().CanOpenLogs);
    }

    private SettingsPageModel CreateModel() => new(_appearance, _keyService, _registry, _navigation, _folders, _logSharer, new TestPaths(_directory), _dialogs,
        ErrorAlerts.Create(_dialogs), new ChatModelCatalogService(_router, NullLogger<ChatModelCatalogService>.Instance), _defaultAssistantModel,
        _semanticIndex, _mainThread);

    private string[] CreateLogFiles(params string[] names)
    {
        var logDirectory = ImageGenLogging.LogDirectoryFor(_directory);
        Directory.CreateDirectory(logDirectory);
        return names.Select(name =>
        {
            var path = Path.Combine(logDirectory, name);
            File.WriteAllText(path, name);
            return path;
        }).ToArray();
    }

    private sealed record TestPaths(string DataDirectory) : IAppPaths;
}

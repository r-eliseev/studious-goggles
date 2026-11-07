using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Search;
using ImageGen.Core.Services;
using ImageGen.Presentation.Appearance;
using ImageGen.Presentation.Logging;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.PageModels;

/// <summary>
/// The settings screen: provider keys, the appearance (style and theme, saved at once)
/// and the logs folder where the shell can open it.
/// </summary>
public sealed partial class SettingsPageModel(
    AppearanceService appearance,
    ApiKeyService apiKeys,
    ProviderRegistry providers,
    INavigationService navigation,
    IFolderLauncher folders,
    ILogSharer logSharer,
    IAppPaths paths,
    IDialogService dialogs,
    ErrorAlert errorAlert,
    ChatModelCatalogService chatModels,
    IDefaultAssistantModel defaultAssistantModel,
    ISemanticIndexController semanticIndex,
    IMainThread mainThread,
    HistoryService history,
    ICurrentSessionTracker currentSessionTracker) : ObservableObject, IDisposable
{
    private readonly ErrorAlert _errorAlert = errorAlert;
    private bool _listeningForSemanticStatus;
    /// <summary>The style cards; built on the first <see cref="LoadCommand"/>, because they refer to this page model.</summary>
    public ObservableCollection<StyleOptionViewModel> Styles { get; } = [];

    /// <summary>Configured providers and their masked key status, in registry order.</summary>
    public ObservableCollection<ProviderKeyItemViewModel> ProviderKeys { get; } = [];

    public ObservableCollection<AssistantModelOption> AssistantModels { get; } = [];

    [ObservableProperty]
    public partial AssistantModelOption? SelectedAssistantModel { get; set; }

    [ObservableProperty]
    public partial SemanticIndexStatus SemanticIndexStatus { get; set; } = new(false, 0, 0, SemanticIndexState.Off);

    public bool IsSemanticSearchEnabled => SemanticIndexStatus.Enabled;

    public bool IsSemanticIndexing => SemanticIndexStatus.State == SemanticIndexState.Indexing;

    public string SemanticSearchToggleText => !IsSemanticSearchEnabled
        ? AppResources.SemanticSearchEnable
        : SemanticIndexStatus.State is SemanticIndexState.Failed or SemanticIndexState.Unavailable
            ? AppResources.SemanticSearchRetry
            : AppResources.SemanticSearchDisable;

    public string SemanticSearchStatusText
    {
        get
        {
            var message = SemanticIndexStatus.State switch
            {
                SemanticIndexState.Off => AppResources.SemanticSearchOff,
                SemanticIndexState.Indexing => string.Format(AppResources.SemanticSearchProgressFormat, SemanticIndexStatus.Indexed, SemanticIndexStatus.Indexed + SemanticIndexStatus.Pending),
                SemanticIndexState.Ready => string.Format(AppResources.SemanticSearchReadyFormat, SemanticIndexStatus.Indexed),
                SemanticIndexState.Unavailable => AppResources.SemanticSearchUnavailable,
                _ => SemanticIndexStatus.ErrorCode == "stopped" ? AppResources.SemanticSearchStopped : AppResources.SemanticSearchFailed,
            };
            if (SemanticIndexStatus.State == SemanticIndexState.Failed && !string.IsNullOrWhiteSpace(SemanticIndexStatus.ErrorDetail))
            {
                message = $"{message}\n{SemanticIndexStatus.ErrorDetail}";
            }

            return SemanticIndexStatus.WarningCode == "limit_reached"
                ? $"{message} {AppResources.SemanticSearchLimitWarning}"
                : message;
        }
    }


    /// <summary>The theme in effect; the radio buttons show it. Changed through <see cref="SelectThemeCommand"/>.</summary>
    [ObservableProperty]
    private AppearanceTheme _selectedTheme;

    public bool IsSystemTheme => SelectedTheme == AppearanceTheme.System;

    public bool IsLightTheme => SelectedTheme == AppearanceTheme.Light;

    public bool IsDarkTheme => SelectedTheme == AppearanceTheme.Dark;

    public bool CanOpenLogs => folders.IsSupported;

    public bool CanShareLogs => logSharer.IsSupported;

    /// <summary>Folder that holds the log files (the same folder the app writes to).</summary>
    public string LogsFolder => ImageGenLogging.LogDirectoryFor(paths.DataDirectory);

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!_listeningForSemanticStatus)
        {
            semanticIndex.StatusChanged += OnSemanticIndexStatusChanged;
            _listeningForSemanticStatus = true;
        }

        UpdateSemanticStatus(await semanticIndex.GetStatusAsync(CancellationToken.None));
        if (Styles.Count == 0)
        {
            Styles.Add(new StyleOptionViewModel(AppearanceStyle.Quiet, AppResources.StyleQuiet, AppResources.StyleQuietHint, SelectStyleAsync));
            Styles.Add(new StyleOptionViewModel(AppearanceStyle.Gallery, AppResources.StyleGallery, AppResources.StyleGalleryHint, SelectStyleAsync));
            Styles.Add(new StyleOptionViewModel(AppearanceStyle.Notebook, AppResources.StyleNotebook, AppResources.StyleNotebookHint, SelectStyleAsync));
        }

        ShowAppearance(appearance.Current);

        if (ProviderKeys.Count == 0)
        {
            foreach (var provider in providers.All)
            {
                ProviderKeys.Add(new ProviderKeyItemViewModel(provider.Id, provider.DisplayName, apiKeys, navigation, dialogs, _errorAlert));
            }
        }

        foreach (var key in ProviderKeys)
        {
            await key.RefreshAsync();
        }

        var assistants = await chatModels.ListAllAssistantModelsAsync(forceRefresh: false, CancellationToken.None);
        AssistantModels.Clear();
        foreach (var (providerId, model) in assistants)
        {
            var providerName = providers.TryGet(providerId, out var provider) ? provider.DisplayName : providerId;
            AssistantModels.Add(new AssistantModelOption(new ModelRef(providerId, model.Id), model.Name, providerName));
        }

        var selectedAssistant = AssistantModels.FirstOrDefault(option => option.Reference == defaultAssistantModel.Current);
        if (selectedAssistant is null)
        {
            var defaultModel = defaultAssistantModel.Current;
            selectedAssistant = new AssistantModelOption(defaultModel,
                defaultModel.ModelId,
                providers.TryGet(defaultModel.ProviderId, out var currentProvider) ? currentProvider.DisplayName : defaultModel.ProviderId);
            AssistantModels.Insert(0, selectedAssistant);
        }

        SelectedAssistantModel = selectedAssistant;
    }

    [RelayCommand]
    private Task OpenLogsAsync() => folders.OpenAsync(LogsFolder, CancellationToken.None);

    [RelayCommand]
    private async Task ShareLogsAsync()
    {
        try
        {
            if (LogFiles.List(LogsFolder).Count == 0)
            {
                await dialogs.AlertAsync(AppResources.SettingsLogs, AppResources.SettingsNoLogs);
                return;
            }

            var targetDirectory = Path.Combine(Path.GetTempPath(), "imagegen-logs-share");
            var copies = await LogFiles.CopyForSharingAsync(LogsFolder, targetDirectory, CancellationToken.None);
            await logSharer.ShareAsync(copies, CancellationToken.None);
        }
        catch (Exception exception)
        {
            await _errorAlert.ShowAsync(exception);
        }
    }

    [RelayCommand]
    private async Task ClearLogsAsync()
    {
        try
        {
            var confirmed = await dialogs.ConfirmAsync(
                AppResources.SettingsLogs,
                AppResources.SettingsClearLogsConfirm,
                AppResources.SettingsClearLogs,
                AppResources.Cancel,
                destructive: true);
            if (!confirmed)
            {
                return;
            }

            var deleted = LogFiles.DeleteAllExceptNewest(LogsFolder);
            await dialogs.AlertAsync(AppResources.SettingsLogs, string.Format(AppResources.SettingsLogsCleared, deleted));
        }
        catch (Exception exception)
        {
            await _errorAlert.ShowAsync(exception);
        }
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        try
        {
            var confirmed = await dialogs.ConfirmAsync(
                AppResources.SettingsClearHistory,
                AppResources.SettingsClearHistoryConfirm,
                AppResources.SettingsClearHistory,
                AppResources.Cancel,
                destructive: true);
            if (!confirmed)
            {
                return;
            }

            var deleted = await history.ClearAllAsync(CancellationToken.None);
            currentSessionTracker.CurrentId = null;
            await navigation.GoToAsync(Routes.Sessions);
            await dialogs.AlertAsync(
                AppResources.SettingsClearHistory,
                string.Format(System.Globalization.CultureInfo.CurrentCulture, AppResources.SettingsHistoryDeletedFormat, deleted));
        }
        catch (Exception exception)
        {
            await _errorAlert.ShowAsync(exception);
        }
    }

    [RelayCommand]
    private Task OpenSpendingAsync() => navigation.GoToAsync(Routes.Spending);

    [RelayCommand]
    private async Task ToggleSemanticSearchAsync()
    {
        try
        {
            if (SemanticIndexStatus.Enabled
                && SemanticIndexStatus.State is (SemanticIndexState.Failed or SemanticIndexState.Unavailable))
            {
                await semanticIndex.EnableAsync(CancellationToken.None);
            }
            else if (SemanticIndexStatus.Enabled)
            {
                var disable = await dialogs.ConfirmAsync(AppResources.SemanticSearchTitle, AppResources.SemanticSearchDisablePrompt,
                    AppResources.SemanticSearchDisable, AppResources.Cancel, destructive: true);
                if (disable)
                {
                    await semanticIndex.DisableAsync(CancellationToken.None);
                }
            }
            else
            {
                var count = await semanticIndex.GetSourceCountAsync(CancellationToken.None);
                var message = string.Format(AppResources.SemanticSearchEnablePromptFormat,
                    Math.Min(count, SemanticIndexService.MaximumIndexedSources), count);
                var enable = await dialogs.ConfirmAsync(AppResources.SemanticSearchTitle, message,
                    AppResources.SemanticSearchEnable, AppResources.Cancel);
                if (enable)
                {
                    await semanticIndex.EnableAsync(CancellationToken.None);
                }
            }

            UpdateSemanticStatus(await semanticIndex.GetStatusAsync(CancellationToken.None));
        }
        catch (Exception exception)
        {
            await _errorAlert.ShowAsync(exception);
        }
    }

    [RelayCommand]
    private async Task StopSemanticIndexAsync()
    {
        try
        {
            await semanticIndex.StopAsync(CancellationToken.None);
            UpdateSemanticStatus(await semanticIndex.GetStatusAsync(CancellationToken.None));
        }
        catch (Exception exception)
        {
            await _errorAlert.ShowAsync(exception);
        }
    }

    private void OnSemanticIndexStatusChanged(object? sender, SemanticIndexStatus status) =>
        mainThread.BeginInvokeOnMainThread(() => UpdateSemanticStatus(status));

    private void UpdateSemanticStatus(SemanticIndexStatus status)
    {
        SemanticIndexStatus = status;
        OnPropertyChanged(nameof(IsSemanticSearchEnabled));
        OnPropertyChanged(nameof(IsSemanticIndexing));
        OnPropertyChanged(nameof(SemanticSearchStatusText));
        OnPropertyChanged(nameof(SemanticSearchToggleText));
    }

    public void Dispose()
    {
        OnDisappearing();
    }

    public void OnDisappearing()
    {
        if (_listeningForSemanticStatus)
        {
            semanticIndex.StatusChanged -= OnSemanticIndexStatusChanged;
            _listeningForSemanticStatus = false;
        }
    }

    [RelayCommand]
    private async Task SaveDefaultAssistantModelAsync()
    {
        var selected = SelectedAssistantModel;
        if (selected is null) return;
        try
        {
            await defaultAssistantModel.SaveAsync(selected.Reference, CancellationToken.None);
        }
        catch (Exception ex)
        {
            await _errorAlert.ShowAsync(ex);
        }
    }

    [RelayCommand]
    private async Task SelectThemeAsync(AppearanceTheme theme)
    {
        await appearance.SaveAsync(appearance.Current with { Theme = theme }, CancellationToken.None);
        ShowAppearance(appearance.Current);
    }

    private async Task SelectStyleAsync(AppearanceStyle style)
    {
        await appearance.SaveAsync(appearance.Current with { Style = style }, CancellationToken.None);
        ShowAppearance(appearance.Current);
    }

    private void ShowAppearance(AppearanceSettings settings)
    {
        foreach (var style in Styles)
        {
            style.IsSelected = style.Style == settings.Style;
        }

        SelectedTheme = settings.Theme;
    }

    partial void OnSelectedThemeChanged(AppearanceTheme value)
    {
        OnPropertyChanged(nameof(IsSystemTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
    }
}

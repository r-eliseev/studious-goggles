using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Platform;
using ImageGen.Presentation.Appearance;
using ImageGen.Presentation.Logging;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.PageModels;

/// <summary>
/// The settings screen: the OpenRouter key (last four characters only), the appearance (style and theme, saved at once)
/// and the logs folder where the shell can open it.
/// </summary>
public sealed partial class SettingsPageModel(
    AppearanceService appearance,
    ISecretStore secrets,
    INavigationService navigation,
    IFolderLauncher folders,
    IAppPaths paths) : ObservableObject
{
    private const int KeyTailLength = 4;

    /// <summary>The style cards; built on the first <see cref="LoadCommand"/>, because they refer to this page model.</summary>
    public ObservableCollection<StyleOptionViewModel> Styles { get; } = [];

    [ObservableProperty]
    private string _keyText = "";

    /// <summary>The theme in effect; the radio buttons show it. Changed through <see cref="SelectThemeCommand"/>.</summary>
    [ObservableProperty]
    private AppearanceTheme _selectedTheme;

    public bool IsSystemTheme => SelectedTheme == AppearanceTheme.System;

    public bool IsLightTheme => SelectedTheme == AppearanceTheme.Light;

    public bool IsDarkTheme => SelectedTheme == AppearanceTheme.Dark;

    public bool CanOpenLogs => folders.IsSupported;

    /// <summary>Folder that holds the log files (the same folder the app writes to).</summary>
    public string LogsFolder => ImageGenLogging.LogDirectoryFor(paths.DataDirectory);

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (Styles.Count == 0)
        {
            Styles.Add(new StyleOptionViewModel(AppearanceStyle.Quiet, AppResources.StyleQuiet, AppResources.StyleQuietHint, SelectStyleAsync));
            Styles.Add(new StyleOptionViewModel(AppearanceStyle.Gallery, AppResources.StyleGallery, AppResources.StyleGalleryHint, SelectStyleAsync));
            Styles.Add(new StyleOptionViewModel(AppearanceStyle.Notebook, AppResources.StyleNotebook, AppResources.StyleNotebookHint, SelectStyleAsync));
        }

        var key = await secrets.GetAsync(SecretNames.OpenRouterApiKey, CancellationToken.None);
        KeyText = string.IsNullOrEmpty(key)
            ? AppResources.SettingsKeyMissing
            : string.Format(CultureInfo.CurrentCulture, AppResources.SettingsKeyConnectedFormat, Tail(key));

        ShowAppearance(appearance.Current);
    }

    [RelayCommand]
    private Task ChangeKeyAsync() => navigation.GoToAsync(Routes.ApiKey);

    [RelayCommand]
    private Task OpenLogsAsync() => folders.OpenAsync(LogsFolder, CancellationToken.None);

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

    private static string Tail(string key) => key[^Math.Min(KeyTailLength, key.Length)..];
}

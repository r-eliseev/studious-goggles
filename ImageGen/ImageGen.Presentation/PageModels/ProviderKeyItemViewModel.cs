using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.PageModels;

/// <summary>One configured provider key on the settings page.</summary>
public sealed partial class ProviderKeyItemViewModel : ObservableObject
{
    private readonly ApiKeyService _keys;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly ErrorAlert _errorAlert;
    private readonly bool _isOpenRouter;

    public ProviderKeyItemViewModel(
        string providerId,
        string displayName,
        ApiKeyService keys,
        INavigationService navigation,
        IDialogService dialogs,
        ErrorAlert errorAlert)
    {
        ProviderId = providerId;
        DisplayName = displayName;
        _keys = keys;
        _navigation = navigation;
        _dialogs = dialogs;
        _errorAlert = errorAlert;
        _isOpenRouter = providerId == ProviderIds.OpenRouter;
    }

    public string ProviderId { get; }

    public string DisplayName { get; }

    [ObservableProperty]
    public partial string KeyText { get; private set; } = AppResources.SettingsKeyMissing;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    public partial bool HasKey { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    public partial bool IsBusy { get; private set; }

    public Task RefreshAsync() => RefreshCoreAsync();

    [RelayCommand]
    private Task ChangeAsync() => _navigation.GoToAsync(Routes.ApiKeyFor(ProviderId));

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private async Task RemoveAsync()
    {
        if (!HasKey || IsBusy)
        {
            return;
        }

        var message = string.Format(CultureInfo.CurrentCulture, AppResources.ProviderKeyRemoveConfirmFormat, DisplayName);
        if (_isOpenRouter)
        {
            message += Environment.NewLine + AppResources.ProviderKeyRemoveOpenRouterWarning;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            AppResources.ProviderKeyRemoveTitle,
            message,
            AppResources.ProviderKeyRemove,
            AppResources.Cancel);
        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _keys.RemoveAsync(ProviderId, CancellationToken.None);
            await RefreshCoreAsync();
        }
        catch (Exception ex)
        {
            await _errorAlert.ShowAsync(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRemove() => HasKey && !IsBusy;

    private async Task RefreshCoreAsync()
    {
        var masked = await _keys.GetMaskedKeyAsync(ProviderId, CancellationToken.None);
        HasKey = masked is not null;
        KeyText = masked is null
            ? AppResources.SettingsKeyMissing
            : string.Format(CultureInfo.CurrentCulture, AppResources.SettingsKeyConnectedFormat, masked[^Math.Min(4, masked.Length)..]);
    }
}

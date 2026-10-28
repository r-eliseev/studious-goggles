using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.PageModels;

public sealed partial class ApiKeyPageModel(
    ApiKeyService apiKeyService,
    INavigationService navigation,
    IDialogService dialogs,
    ISecretPersistence persistence) : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string ApiKey { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>Result of the last check. The page announces changes to screen readers.</summary>
    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    /// <summary>Shown when the key will not survive closing the app; empty otherwise.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPersistenceWarning))]
    public partial string PersistenceWarning { get; set; } = "";

    /// <summary>True when the key would not be kept safely; the page shows <see cref="PersistenceWarning"/>.</summary>
    public bool HasPersistenceWarning => PersistenceWarning.Length > 0;

    /// <summary>True when the last check failed (rejected key or no network), so the field shows the error.</summary>
    [ObservableProperty]
    public partial bool HasError { get; set; }

    /// <summary>Checks where the key would be kept. The page calls it when it appears.</summary>
    public async Task OnAppearingAsync()
    {
        var persistent = await persistence.IsPersistentAsync(CancellationToken.None);
        PersistenceWarning = persistent ? "" : AppResources.ApiKeyNotPersisted;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        IsBusy = true;
        HasError = false;
        StatusText = AppResources.ApiKeyChecking;
        try
        {
            var status = await apiKeyService.SaveAsync(ProviderIds.OpenRouter, ApiKey, CancellationToken.None);
            if (status == ApiKeyStatus.Valid)
            {
                ApiKey = "";
                StatusText = "";
                await navigation.GoBackAsync();
                return;
            }

            HasError = true;
            StatusText = status switch
            {
                ApiKeyStatus.Invalid => AppResources.ApiKeyInvalid,
                _ => AppResources.ApiKeyNetworkError,
            };
        }
        catch (Exception ex)
        {
            StatusText = "";
            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSave() => !IsBusy && !string.IsNullOrWhiteSpace(ApiKey);
}

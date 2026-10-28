using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Services;
using ImageGen.Maui.Resources.Strings;
using ImageGen.Maui.Services;

namespace ImageGen.Maui.PageModels;

public sealed partial class ApiKeyPageModel(
    ApiKeyService apiKeyService,
    INavigationService navigation,
    IDialogService dialogs) : ObservableObject
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

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        IsBusy = true;
        StatusText = AppResources.ApiKeyChecking;
        try
        {
            var status = await apiKeyService.SaveAsync(ApiKey, CancellationToken.None);
            if (status == ApiKeyStatus.Valid)
            {
                ApiKey = "";
                StatusText = "";
                await navigation.GoBackAsync();
                return;
            }

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

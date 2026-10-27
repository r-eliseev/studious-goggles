using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Services;
using ImageGen.Maui.Resources.Strings;
using ImageGen.Maui.Services;

namespace ImageGen.Maui.PageModels;

public sealed partial class ApiKeyPageModel(ApiKeyService apiKeyService, INavigationService navigation) : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string ApiKey { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    private bool CanSave() => !IsBusy && !string.IsNullOrWhiteSpace(ApiKey);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        IsBusy = true;
        StatusText = AppResources.ApiKeyChecking;
        try
        {
            var status = await apiKeyService.SaveAsync(ApiKey, CancellationToken.None);
            switch (status)
            {
                case ApiKeyStatus.Valid:
                    ApiKey = string.Empty;
                    StatusText = string.Empty;
                    await navigation.GoBackAsync();
                    break;
                case ApiKeyStatus.Invalid:
                    StatusText = AppResources.ApiKeyInvalid;
                    break;
                default:
                    StatusText = AppResources.ApiKeyNetworkError;
                    break;
            }

            SemanticScreenReader.Announce(StatusText);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

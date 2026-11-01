using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.PageModels;

public sealed partial class ApiKeyPageModel(
    ApiKeyService apiKeyService,
    ProviderRegistry providers,
    INavigationService navigation,
    IDialogService dialogs,
    ISecretPersistence persistence,
    ErrorAlert errorAlert,
    ChatModelCatalogService chatModels,
    IDefaultAssistantModel defaultAssistantModel) : ObservableObject
{
    private readonly ErrorAlert _errorAlert = errorAlert;
    private bool _invalidRouteAlertShown;

    public ObservableCollection<AssistantProviderOption> ChatProviders { get; } = [];

    public ObservableCollection<AssistantModelOption> AssistantModels { get; } = [];

    [ObservableProperty]
    public partial bool IsFirstRun { get; private set; }

    [ObservableProperty]
    public partial bool IsChoosingAssistantModel { get; private set; }

    [ObservableProperty]
    public partial AssistantProviderOption? SelectedChatProvider { get; set; }

    [ObservableProperty]
    public partial AssistantModelOption? SelectedAssistantModel { get; set; }

    public bool HasAssistantModels => AssistantModels.Count > 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string? ProviderId { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyPropertyChangedFor(nameof(ProviderTitle))]
    [NotifyPropertyChangedFor(nameof(ProviderDescription))]
    public partial string ProviderDisplayName { get; private set; } = "";

    [ObservableProperty]
    public partial bool IsRouteInvalid { get; private set; }

    public string ProviderTitle => string.IsNullOrEmpty(ProviderDisplayName)
        ? AppResources.ApiKeyHeading
        : string.Format(CultureInfo.CurrentCulture, AppResources.ProviderKeyTitleFormat, ProviderDisplayName);

    public string ProviderDescription => string.IsNullOrEmpty(ProviderDisplayName)
        ? AppResources.ApiKeyDescription
        : string.Format(CultureInfo.CurrentCulture, AppResources.ProviderKeyDescriptionFormat, ProviderDisplayName);

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

    /// <summary>True when the key would not be kept safely; the page shows the warning.</summary>
    public bool HasPersistenceWarning => PersistenceWarning.Length > 0;

    /// <summary>True when the last check failed (rejected key or no network), so the field shows the error.</summary>
    [ObservableProperty]
    public partial bool HasError { get; set; }

    /// <summary>Receives the decoded provider id from the shell route.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        ProviderId = null;
        ProviderDisplayName = "";
        ApiKey = "";
        StatusText = "";
        HasError = false;
        PersistenceWarning = "";
        IsChoosingAssistantModel = false;
        AssistantModels.Clear();
        IsFirstRun = ReadFirstRun(query);
        ChatProviders.Clear();
        foreach (var chatProviderId in providers.All.Where(IsChatProvider))
        {
            ChatProviders.Add(new AssistantProviderOption(chatProviderId.Id, chatProviderId.DisplayName));
        }
        IsRouteInvalid = !query.TryGetValue(Routes.ProviderIdParameter, out var value)
            || value is not string providerId
            || !providers.TryGet(providerId, out _);
        if (!IsRouteInvalid && value is string validProviderId && providers.TryGet(validProviderId, out var provider))
        {
            ProviderId = provider.Id;
            ProviderDisplayName = provider.DisplayName;
            SelectedChatProvider = ChatProviders.FirstOrDefault(option => option.Id == provider.Id);
        }

        _invalidRouteAlertShown = false;
    }

    private static bool ReadFirstRun(IDictionary<string, object> query) =>
        query.TryGetValue(Routes.FirstRunParameter, out var value)
        && string.Equals(value?.ToString(), "true", StringComparison.OrdinalIgnoreCase);

    partial void OnSelectedChatProviderChanged(AssistantProviderOption? value)
    {
        if (!IsFirstRun || value is null || !providers.TryGet(value.Id, out var provider)) return;
        ProviderId = provider.Id;
        ProviderDisplayName = provider.DisplayName;
        ApiKey = "";
        StatusText = "";
        HasError = false;
    }

    /// <summary>Checks persistence and safely rejects a missing/unknown provider route once.</summary>
    public async Task OnAppearingAsync()
    {
        if (IsRouteInvalid || ProviderId is null)
        {
            if (!_invalidRouteAlertShown)
            {
                _invalidRouteAlertShown = true;
                await dialogs.AlertAsync(AppResources.ErrorTitle, AppResources.ProviderKeyUnknown);
                await navigation.GoBackAsync();
            }

            return;
        }

        var persistent = await persistence.IsPersistentAsync(CancellationToken.None);
        PersistenceWarning = persistent ? "" : AppResources.ApiKeyNotPersisted;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var providerId = ProviderId;
        if (providerId is null || !providers.TryGet(providerId, out var provider))
        {
            return;
        }

        IsBusy = true;
        HasError = false;
        StatusText = AppResources.ApiKeyChecking;
        try
        {
            var status = await apiKeyService.SaveAsync(providerId, ApiKey, CancellationToken.None);
            if (status == ApiKeyStatus.Valid)
            {
                ApiKey = "";
                StatusText = "";
                if (IsFirstRun && providerId != defaultAssistantModel.Current.ProviderId)
                {
                    var models = await chatModels.ListAssistantModelsAsync(providerId, CancellationToken.None);
                    AssistantModels.Clear();
                    foreach (var model in models)
                    {
                        AssistantModels.Add(new AssistantModelOption(new ModelRef(providerId, model.Id), model.Name, provider.DisplayName));
                    }

                    SelectedAssistantModel = AssistantModels.FirstOrDefault(option => option.Reference == defaultAssistantModel.Current);
                    IsChoosingAssistantModel = true;
                    OnPropertyChanged(nameof(HasAssistantModels));
                    if (AssistantModels.Count == 0)
                    {
                        StatusText = AppResources.NoAssistantModelsConfigured;
                    }

                    return;
                }

                await navigation.GoBackAsync();
                return;
            }

            HasError = true;
            StatusText = status switch
            {
                ApiKeyStatus.Invalid => string.Format(CultureInfo.CurrentCulture, AppResources.ProviderKeyInvalidFormat, provider.DisplayName),
                _ => string.Format(CultureInfo.CurrentCulture, AppResources.ProviderKeyNetworkErrorFormat, provider.DisplayName),
            };
        }
        catch (Exception ex)
        {
            StatusText = "";
            await _errorAlert.ShowAsync(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UseSelectedAssistantModelAsync()
    {
        if (SelectedAssistantModel is null || !IsChoosingAssistantModel) return;
        try
        {
            await defaultAssistantModel.SaveAsync(SelectedAssistantModel.Reference, CancellationToken.None);
            await navigation.GoBackAsync();
        }
        catch (Exception ex)
        {
            await _errorAlert.ShowAsync(ex);
        }
    }

    private bool CanSave() => ProviderId is not null && !IsRouteInvalid && !IsBusy && !string.IsNullOrWhiteSpace(ApiKey);

    private static bool IsChatProvider(ProviderConfig provider) =>
        provider.Kind == ProviderKind.OpenRouter
        || (provider.Kind is ProviderKind.OpenAiCompatible or ProviderKind.AnthropicCompatible) && provider.ChatModels is not null;
}

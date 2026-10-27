using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Services;
using ImageGen.Maui.Resources.Strings;
using ImageGen.Maui.Services;

namespace ImageGen.Maui.PageModels;

public sealed partial class SessionsPageModel(
    AppInitializer initializer,
    ApiKeyService apiKeyService,
    SessionService sessionService,
    INavigationService navigation,
    IDialogService dialogs) : ObservableObject
{
    public ObservableCollection<SessionItemViewModel> Sessions { get; } = [];

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Called by the page on every appearance.</summary>
    public async Task OnAppearingAsync()
    {
        try
        {
            IsBusy = true;
            await initializer.EnsureInitializedAsync();
            if (!await apiKeyService.HasKeyAsync(CancellationToken.None))
            {
                await navigation.GoToAsync(Routes.ApiKey);
                return;
            }

            var sessions = await sessionService.ListAsync(CancellationToken.None);
            Sessions.Clear();
            foreach (var session in sessions)
            {
                Sessions.Add(new SessionItemViewModel(session, OpenSessionAsync, DeleteSessionAsync));
            }
        }
        catch (Exception ex)
        {
            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task NewSessionAsync()
    {
        try
        {
            var session = await sessionService.CreateAsync(CancellationToken.None);
            await navigation.GoToAsync(Routes.ChatFor(session.Id));
        }
        catch (Exception ex)
        {
            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
        }
    }

    private Task OpenSessionAsync(SessionItemViewModel item) => navigation.GoToAsync(Routes.ChatFor(item.Id));

    private async Task DeleteSessionAsync(SessionItemViewModel item)
    {
        var confirmed = await dialogs.ConfirmAsync(
            AppResources.DeleteSessionTitle,
            AppResources.DeleteSessionMessage,
            AppResources.DeleteSession,
            AppResources.Cancel);
        if (!confirmed)
        {
            return;
        }

        try
        {
            await sessionService.DeleteAsync(item.Id, CancellationToken.None);
            Sessions.Remove(item);
        }
        catch (Exception ex)
        {
            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
        }
    }
}

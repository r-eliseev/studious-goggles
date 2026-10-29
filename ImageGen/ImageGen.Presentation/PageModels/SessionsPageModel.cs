using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.PageModels;

public sealed partial class SessionsPageModel(
    AppInitializer initializer,
    ApiKeyService apiKeyService,
    SessionService sessionService,
    INavigationService navigation,
    IDialogService dialogs,
    TimeProvider timeProvider,
    ICurrentSessionTracker tracker) : ObservableObject
{
    public ObservableCollection<SessionItemViewModel> Sessions { get; } = [];

    /// <summary>Sessions last changed today (local date), newest first.</summary>
    public ObservableCollection<SessionItemViewModel> Today { get; } = [];

    /// <summary>Sessions last changed on earlier days, newest first.</summary>
    public ObservableCollection<SessionItemViewModel> Earlier { get; } = [];

    /// <summary>
    /// True while the list sits beside a chat: opening a session replaces that chat, so going back returns to the list.
    /// The views set it when they appear.
    /// </summary>
    public bool ReplaceOnOpen { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    private void OnCurrentSessionChanged(Guid? current)
    {
        foreach (var item in Sessions)
        {
            item.IsCurrent = item.Id == current;
        }
    }

    /// <summary>Called by the page on every appearance: initializes the app once, then lists the sessions.</summary>
    public async Task OnAppearingAsync()
    {
        tracker.Changed -= OnCurrentSessionChanged;
        tracker.Changed += OnCurrentSessionChanged;
        IsBusy = true;
        try
        {
            await initializer.EnsureInitializedAsync();
            if (!await apiKeyService.HasKeyAsync(ProviderIds.OpenRouter, CancellationToken.None))
            {
                await navigation.GoToAsync(Routes.ApiKey);
                return;
            }

            var sessions = await sessionService.ListAsync(CancellationToken.None);
            var runCounts = await sessionService.CountRunsAsync(CancellationToken.None);
            var today = timeProvider.GetLocalNow().Date;
            Sessions.Clear();
            Today.Clear();
            Earlier.Clear();
            foreach (var session in sessions.OrderByDescending(session => session.UpdatedAt))
            {
                var item = new SessionItemViewModel(session, OpenSessionAsync, RenameSessionAsync, DeleteSessionAsync)
                {
                    IsCurrent = session.Id == tracker.CurrentId,
                    RunCount = runCounts.GetValueOrDefault(session.Id),
                };
                Sessions.Add(item);
                (session.UpdatedAt.ToLocalTime().Date == today ? Today : Earlier).Add(item);
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
    private Task OpenSettingsAsync() => navigation.GoToAsync(Routes.Settings);

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

    private async Task OpenSessionAsync(SessionItemViewModel item)
    {
        try
        {
            if (ReplaceOnOpen)
            {
                await navigation.ReplaceAsync(Routes.ChatFor(item.Id));
            }
            else
            {
                await navigation.GoToAsync(Routes.ChatFor(item.Id));
            }
        }
        catch (Exception ex)
        {
            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
        }
    }

    private async Task RenameSessionAsync(SessionItemViewModel item)
    {
        try
        {
            var title = await dialogs.PromptAsync(
                AppResources.RenameSessionTitle, "", AppResources.Rename, AppResources.Cancel, item.Title);
            if (title is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                await dialogs.AlertAsync(AppResources.ErrorTitle, AppResources.RenameEmpty);
                return;
            }

            var session = await sessionService.RenameAsync(item.Id, title, CancellationToken.None);
            item.Title = session.Title;
        }
        catch (Exception ex)
        {
            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
        }
    }

    private async Task DeleteSessionAsync(SessionItemViewModel item)
    {
        var confirmed = await dialogs.ConfirmAsync(
            AppResources.DeleteSessionTitle,
            AppResources.DeleteSessionMessage,
            AppResources.DeleteSession,
            AppResources.Cancel,
            destructive: true);
        if (!confirmed)
        {
            return;
        }

        try
        {
            await sessionService.DeleteAsync(item.Id, CancellationToken.None);
            Sessions.Remove(item);
            Today.Remove(item);
            Earlier.Remove(item);
        }
        catch (Exception ex)
        {
            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
        }
    }
}

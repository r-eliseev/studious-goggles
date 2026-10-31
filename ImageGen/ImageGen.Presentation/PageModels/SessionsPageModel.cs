using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Search;
using ImageGen.Core.Services;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Utilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Presentation.PageModels;

public sealed partial class SessionsPageModel(
    AppInitializer initializer,
    ApiKeyService apiKeyService,
    SessionService sessionService,
    INavigationService navigation,
    IDialogService dialogs,
    TimeProvider timeProvider,
    ICurrentSessionTracker tracker,
    SessionSearchService searchService,
    SessionChangeNotifier changes,
    IMainThread mainThread,
    ErrorAlert? errorAlert = null) : ObservableObject
{
    private readonly ErrorAlert _errorAlert = errorAlert ?? new ErrorAlert(dialogs, NullLogger<ErrorAlert>.Instance);
    /// <summary>Pause after the last keystroke before the search runs.</summary>
    public static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(250);

    public ObservableCollection<SessionItemViewModel> Sessions { get; } = [];

    /// <summary>The starter prompts of the empty state (task 078). Pressing one creates a session and sends the prompt.</summary>
    public IReadOnlyList<string> Examples { get; } =
    [
        AppResources.EmptyChatExample1,
        AppResources.EmptyChatExample2,
        AppResources.EmptyChatExample3,
    ];

    /// <summary>True when there are no sessions.</summary>
    public bool HasNoSessions => Sessions.Count == 0;

    /// <summary>
    /// Set by the view: the narrow layout has no empty-state panel, so the examples go into the list while it is empty.
    /// </summary>
    [ObservableProperty]
    public partial bool ExamplesInList { get; set; }

    /// <summary>True when the narrow layout shows the examples in the list: the flag is set and no session exists.</summary>
    public bool ShowsListExamples => ExamplesInList && HasNoSessions;

    partial void OnExamplesInListChanged(bool value) => OnPropertyChanged(nameof(ShowsListExamples));

    /// <summary>Sessions that match <see cref="SearchText"/>, best match first. Their rows show <see cref="SessionItemViewModel.SearchLine"/>.</summary>
    public ObservableCollection<SessionItemViewModel> SearchResults { get; } = [];

    private CancellationTokenSource? _searchCancellation;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

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

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    /// <summary>True when a non-empty query matched no session.</summary>
    [ObservableProperty]
    public partial bool HasNoResults { get; set; }

    /// <summary>True while the query is not blank: the list then shows the search results instead of the groups.</summary>
    public bool IsSearchActive => !string.IsNullOrWhiteSpace(SearchText);

    /// <summary>The opposite of <see cref="IsSearchActive"/>, for bindings that cannot negate.</summary>
    public bool IsSearchInactive => !IsSearchActive;

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsSearchActive));
        OnPropertyChanged(nameof(IsSearchInactive));
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        var cancellation = _searchCancellation = new CancellationTokenSource();
        SearchAfterDelayAsync(value, cancellation.Token).FireAndForgetSafeAsync();
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = "";
        ClearSearchResults();
    }

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
        changes.SessionChanged -= OnSessionChanged;
        changes.SessionChanged += OnSessionChanged;
        IsBusy = true;
        try
        {
            await initializer.EnsureInitializedAsync();
            if (!await apiKeyService.HasKeyAsync(ProviderIds.OpenRouter, CancellationToken.None))
            {
                await navigation.GoToAsync(Routes.ApiKeyFor(ProviderIds.OpenRouter));
                return;
            }

            await _refreshGate.WaitAsync();
            try
            {
                SearchText = "";
                ClearSearchResults();
                var sessions = await sessionService.ListAsync(CancellationToken.None);
                var runCounts = await sessionService.CountRunsAsync(CancellationToken.None);
                await DispatchAsync(() =>
                {
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
                    NotifySessionsCount();
                });
            }
            finally
            {
                _refreshGate.Release();
            }
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

    private void OnSessionChanged(object? sender, Guid sessionId) =>
        mainThread.BeginInvokeOnMainThread(() => RefreshSessionAsync(sessionId).FireAndForgetSafeAsync());

    private async Task RefreshSessionAsync(Guid sessionId)
    {
        await _refreshGate.WaitAsync();
        try
        {
            var session = await sessionService.GetAsync(sessionId, CancellationToken.None);
            if (session is null)
            {
                await DispatchAsync(() => RemoveSessionRow(sessionId));
                return;
            }

            var runCount = (await sessionService.CountRunsAsync(CancellationToken.None)).GetValueOrDefault(sessionId);
            await DispatchAsync(() => ApplySessionRow(session, runCount));
        }
        catch (Exception ex)
        {
            await _errorAlert.ShowAsync(ex);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task DispatchAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        mainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });
        await completion.Task;
    }

    private void RemoveSessionRow(Guid sessionId)
    {
        var item = Sessions.FirstOrDefault(row => row.Id == sessionId);
        if (item is null)
        {
            return;
        }

        Sessions.Remove(item);
        Today.Remove(item);
        Earlier.Remove(item);
        SearchResults.Remove(item);
        NotifySessionsCount();
    }

    private void ApplySessionRow(Session session, int runCount)
    {
        var item = Sessions.FirstOrDefault(row => row.Id == session.Id);
        if (item is null)
        {
            item = new SessionItemViewModel(session, OpenSessionAsync, RenameSessionAsync, DeleteSessionAsync)
            {
                IsCurrent = session.Id == tracker.CurrentId,
                RunCount = runCount,
            };
            var index = Sessions.TakeWhile(row => row.UpdatedAt >= session.UpdatedAt).Count();
            Sessions.Insert(index, item);
            var group = session.UpdatedAt.ToLocalTime().Date == timeProvider.GetLocalNow().Date ? Today : Earlier;
            var groupIndex = group.TakeWhile(row => row.UpdatedAt >= session.UpdatedAt).Count();
            group.Insert(groupIndex, item);
            NotifySessionsCount();
            return;
        }

        var moved = session.UpdatedAt > item.UpdatedAt;
        item.Apply(session, runCount);
        if (!moved)
        {
            return;
        }

        var sessionIndex = Sessions.IndexOf(item);
        if (sessionIndex > 0)
        {
            Sessions.Move(sessionIndex, 0);
        }
        if (session.UpdatedAt.ToLocalTime().Date != timeProvider.GetLocalNow().Date)
        {
            Today.Remove(item);
            var earlierIndex = Earlier.TakeWhile(row => row.UpdatedAt >= session.UpdatedAt).Count();
            Earlier.Insert(earlierIndex, item);
        }
        else
        {
            Earlier.Remove(item);
            if (!Today.Contains(item))
            {
                Today.Insert(0, item);
            }
            else
            {
                var todayIndex = Today.IndexOf(item);
                if (todayIndex > 0)
                {
                    Today.Move(todayIndex, 0);
                }
            }
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
            await _errorAlert.ShowAsync(ex);
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
            await _errorAlert.ShowAsync(ex);
        }
    }

    private async Task SearchAfterDelayAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SearchDebounce, timeProvider, cancellationToken);
            await ShowSearchResultsAsync(query, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer keystroke replaced this search.
        }
        catch (Exception ex)
        {
            await _errorAlert.ShowAsync(ex);
        }
    }

    private async Task ShowSearchResultsAsync(string query, CancellationToken cancellationToken)
    {
        ClearSearchResults();
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        IsSearching = true;
        try
        {
            var hits = await searchService.SearchAsync(query, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var rows = hits
                .Join(Sessions, hit => hit.SessionId, item => item.Id, (hit, item) => (Hit: hit, Item: item))
                .OrderByDescending(row => row.Hit.Score)
                .ThenBy(row => Sessions.IndexOf(row.Item));
            foreach (var row in rows)
            {
                row.Item.SearchLine = $"{KindLabel(row.Hit.Kind)}: {row.Hit.Snippet}";
                SearchResults.Add(row.Item);
            }
        }
        finally
        {
            IsSearching = false;
        }

        HasNoResults = SearchResults.Count == 0;
    }

    private void ClearSearchResults()
    {
        foreach (var item in Sessions)
        {
            item.SearchLine = "";
        }

        SearchResults.Clear();
        HasNoResults = false;
    }

    private static string KindLabel(SearchTextKind kind) => kind switch
    {
        SearchTextKind.Title => AppResources.SearchInTitle,
        SearchTextKind.Prompt => AppResources.SearchInPrompt,
        _ => AppResources.SearchInDescription,
    };

    private void NotifySessionsCount()
    {
        OnPropertyChanged(nameof(HasNoSessions));
        OnPropertyChanged(nameof(ShowsListExamples));
    }

    /// <summary>Creates a session and opens its chat with the example text, which the chat sends once (task 078).</summary>
    [RelayCommand]
    private async Task StartExampleAsync(string? text)
    {
        try
        {
            var session = await sessionService.CreateAsync(CancellationToken.None);
            await navigation.GoToAsync(Routes.ChatWithMessage(session.Id, text ?? string.Empty));
        }
        catch (Exception ex)
        {
            await _errorAlert.ShowAsync(ex);
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
            await _errorAlert.ShowAsync(ex);
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
            await _refreshGate.WaitAsync();
            try
            {
                await sessionService.DeleteAsync(item.Id, CancellationToken.None);
                await DispatchAsync(() => RemoveSessionRow(item.Id));
            }
            finally
            {
                _refreshGate.Release();
            }
        }
        catch (Exception ex)
        {
            await _errorAlert.ShowAsync(ex);
        }
    }
}

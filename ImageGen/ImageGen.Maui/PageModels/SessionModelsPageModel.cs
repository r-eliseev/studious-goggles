using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Services;
using ImageGen.Maui.Resources.Strings;
using ImageGen.Maui.Services;

namespace ImageGen.Maui.PageModels;

/// <summary>
/// Image models of one session: search, checkboxes, runs per model, refresh from the catalog and save.
/// The page forwards the Shell query to <see cref="ApplyQueryAttributes"/> and calls <see cref="OnAppearingAsync"/>
/// on its first appearance.
/// </summary>
public sealed partial class SessionModelsPageModel(
    SessionService sessionService,
    ModelCatalogService catalog,
    INavigationService navigation,
    IDialogService dialogs) : ObservableObject
{
    private readonly List<ModelOptionViewModel> _all = [];
    private Guid _sessionId;
    private Session? _session;
    private bool _loaded;

    /// <summary>Rows shown now: the catalog filtered by <see cref="SearchText"/>.</summary>
    public ObservableCollection<ModelOptionViewModel> Models { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    /// <summary>
    /// Runs per model. Typed as double because the Stepper binds to a double; the value is always a whole number
    /// from <see cref="MinRunsPerModel"/> to <see cref="MaxRunsPerModel"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RunsPerModelText))]
    public partial double RunsPerModel { get; set; } = SessionSettings.MinRunsPerModel;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsBusy { get; set; }

    public string RunsPerModelText =>
        string.Format(CultureInfo.CurrentCulture, AppResources.RunsPerModelFormat, (int)Math.Round(RunsPerModel));

    public double MinRunsPerModel => SessionSettings.MinRunsPerModel;

    public double MaxRunsPerModel => SessionSettings.MaxRunsPerModel;

    /// <summary>Reads the session id from the route query. The page forwards the Shell query here.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!query.TryGetValue(Routes.SessionIdParameter, out var value)
            || value is not string text
            || !Guid.TryParse(text, out var sessionId))
        {
            throw new ArgumentException(
                $"The models route needs a valid '{Routes.SessionIdParameter}' query parameter.", nameof(query));
        }

        _sessionId = sessionId;
    }

    /// <summary>Loads the session and the catalog from the cache (the cache loads from the API when empty).</summary>
    public Task OnAppearingAsync() => LoadAsync(forceRefresh: false);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task RefreshAsync() => LoadAsync(forceRefresh: true);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task SaveAsync()
    {
        try
        {
            var session = _session ?? throw new InvalidOperationException($"Session {_sessionId} is not loaded.");
            var knownIds = _all.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
            // Models that are saved but not in the catalog stay selected: the catalog may not list them.
            var unknownSelected = session.Settings.ImageModelIds.Where(id => !knownIds.Contains(id));
            var settings = new SessionSettings
            {
                ImageModelIds = _all.Where(m => m.IsSelected).Select(m => m.Id).Concat(unknownSelected).ToList(),
                RunsPerModel = (int)Math.Round(RunsPerModel),
                AssistantModelId = session.Settings.AssistantModelId,
            };
            await sessionService.UpdateSettingsAsync(_sessionId, settings, CancellationToken.None);
            await navigation.GoBackAsync();
        }
        catch (Exception ex)
        {
            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
        }
    }

    private bool CanRun() => !IsBusy;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private async Task LoadAsync(bool forceRefresh)
    {
        IsBusy = true;
        try
        {
            var session = await sessionService.GetAsync(_sessionId, CancellationToken.None)
                ?? throw new InvalidOperationException($"Session {_sessionId} not found.");
            _session = session;

            // After the first load the checkboxes are the source of truth (they may hold unsaved changes).
            var selected = _loaded
                ? _all.Where(m => m.IsSelected).Select(m => m.Id).ToHashSet(StringComparer.Ordinal)
                : session.Settings.ImageModelIds.ToHashSet(StringComparer.Ordinal);
            if (!_loaded)
            {
                RunsPerModel = session.Settings.RunsPerModel;
            }

            var models = await catalog.GetImageModelsAsync(forceRefresh, CancellationToken.None);
            _all.Clear();
            _all.AddRange(models.Select(m => new ModelOptionViewModel(m, selected.Contains(m.Id))));
            _loaded = true;
            ApplyFilter();
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

    private void ApplyFilter()
    {
        var search = SearchText.Trim();
        Models.Clear();
        foreach (var model in _all)
        {
            if (search.Length == 0
                || model.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || model.Id.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                Models.Add(model);
            }
        }
    }
}

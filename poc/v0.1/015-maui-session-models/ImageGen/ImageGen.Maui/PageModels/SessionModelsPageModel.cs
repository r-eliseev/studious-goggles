using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Services;
using ImageGen.Maui.Resources.Strings;
using ImageGen.Maui.Services;

namespace ImageGen.Maui.PageModels;

public sealed partial class SessionModelsPageModel(
    SessionService sessionService,
    ModelCatalogService catalog,
    INavigationService navigation,
    IDialogService dialogs) : ObservableObject, IQueryAttributable
{
    private readonly List<ModelOptionViewModel> _all = [];
    private Session? _session;
    private Guid _sessionId;

    public ObservableCollection<ModelOptionViewModel> Models { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RunsPerModelText))]
    public partial int RunsPerModel { get; set; } = SessionSettings.MinRunsPerModel;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public string RunsPerModelText =>
        string.Format(CultureInfo.CurrentCulture, AppResources.RunsPerModelFormat, RunsPerModel);

    public int MinRunsPerModel => SessionSettings.MinRunsPerModel;

    public int MaxRunsPerModel => SessionSettings.MaxRunsPerModel;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(Routes.SessionIdParameter, out var value)
            && Guid.TryParse(value?.ToString(), out var sessionId))
        {
            _sessionId = sessionId;
        }
    }

    /// <summary>Called by the page when it appears for the first time.</summary>
    public Task OnAppearingAsync() => LoadAsync(forceRefresh: false);

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync(forceRefresh: true);

    private async Task LoadAsync(bool forceRefresh)
    {
        IsBusy = true;
        try
        {
            _session = await sessionService.GetAsync(_sessionId, CancellationToken.None)
                ?? throw new InvalidOperationException($"Session {_sessionId} not found.");
            var selected = _all.Count > 0
                ? _all.Where(m => m.IsSelected).Select(m => m.Id).ToHashSet()
                : _session.Settings.ImageModelIds.ToHashSet();
            if (_all.Count == 0)
            {
                RunsPerModel = _session.Settings.RunsPerModel;
            }

            var models = await catalog.GetImageModelsAsync(forceRefresh, CancellationToken.None);
            _all.Clear();
            _all.AddRange(models.Select(m => new ModelOptionViewModel(m, selected.Contains(m.Id))));
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

    partial void OnSearchTextChanged(string value) => ApplyFilter();

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

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_session is null)
        {
            return;
        }

        try
        {
            var knownIds = _all.Select(m => m.Id).ToHashSet();
            var unknownSelected = _session.Settings.ImageModelIds.Where(id => !knownIds.Contains(id));
            var settings = new SessionSettings
            {
                ImageModelIds = _all.Where(m => m.IsSelected).Select(m => m.Id).Concat(unknownSelected).ToList(),
                RunsPerModel = RunsPerModel,
                AssistantModelId = _session.Settings.AssistantModelId,
            };
            await sessionService.UpdateSettingsAsync(_sessionId, settings, CancellationToken.None);
            await navigation.GoBackAsync();
        }
        catch (Exception ex)
        {
            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
        }
    }
}

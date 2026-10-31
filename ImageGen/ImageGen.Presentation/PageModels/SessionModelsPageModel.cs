using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.PageModels;

/// <summary>
/// Models of one session: search, image model checkboxes, runs per model, the assistant model (one choice from the
/// models that list tools), refresh from the catalog and save.
/// The page forwards the Shell query to <see cref="ApplyQueryAttributes"/> and calls <see cref="OnAppearingAsync"/>
/// on its first appearance.
/// </summary>
public sealed partial class SessionModelsPageModel(
    SessionService sessionService,
    ModelCatalogService catalog,
    ChatModelCatalogService chatModels,
    ProviderRegistry providers,
    INavigationService navigation,
    ErrorAlert errorAlert) : ObservableObject
{
    private readonly ErrorAlert _errorAlert = errorAlert;
    private readonly List<ModelOptionViewModel> _all = [];
    private readonly List<AssistantModelOptionViewModel> _assistants = [];
    private readonly Dictionary<string, bool> _imageGroupExpanded = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _assistantGroupExpanded = new(StringComparer.Ordinal);
    private Guid _sessionId;
    private Session? _session;
    private bool _loaded;

    /// <summary>In pane mode (wide window) Save and Cancel raise <see cref="CloseRequested"/> instead of going back.</summary>
    public bool HostedInPane { get; set; }

    /// <summary>Raised when the pane should close (Save, Cancel or the close button).</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Image model rows shown now: the catalog filtered by <see cref="SearchText"/>.</summary>
    public ObservableCollection<ModelOptionViewModel> Models { get; } = [];

    /// <summary>Assistant model rows shown now: the models with tools filtered by <see cref="SearchText"/>.</summary>
    public ObservableCollection<AssistantModelOptionViewModel> AssistantModels { get; } = [];

    public ObservableCollection<ImageModelGroupViewModel> ImageModelGroups { get; } = [];

    public ObservableCollection<AssistantModelGroupViewModel> AssistantModelGroups { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImageModelsTabSelected))]
    [NotifyPropertyChangedFor(nameof(IsAssistantTabSelected))]
    [NotifyPropertyChangedFor(nameof(SelectedTabIndex))]
    [NotifyPropertyChangedFor(nameof(ImageModelsTabAutomationName))]
    [NotifyPropertyChangedFor(nameof(AssistantTabAutomationName))]
    public partial SessionModelsTab SelectedTab { get; set; } = SessionModelsTab.ImageModels;

    public bool IsImageModelsTabSelected => SelectedTab == SessionModelsTab.ImageModels;

    public bool IsAssistantTabSelected => SelectedTab == SessionModelsTab.AssistantModel;

    public int SelectedTabIndex
    {
        get => (int)SelectedTab;
        set
        {
            if (Enum.IsDefined(typeof(SessionModelsTab), value))
            {
                SelectedTab = (SessionModelsTab)value;
            }
        }
    }

    public string ImageModelsTabAutomationName => IsImageModelsTabSelected
        ? $"{AppResources.ImageModelsTab}, {AppResources.AssistantSelected}"
        : AppResources.ImageModelsTab;

    public string AssistantTabAutomationName => IsAssistantTabSelected
        ? $"{AppResources.AssistantModelTitle}, {AppResources.AssistantSelected}"
        : AppResources.AssistantModelTitle;

    [RelayCommand]
    private void ShowImageModels() => SelectedTab = SessionModelsTab.ImageModels;

    [RelayCommand]
    private void ShowAssistantModel() => SelectedTab = SessionModelsTab.AssistantModel;

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

    /// <summary>"Image models · N selected" for the section header; updated when a row is checked or unchecked.</summary>
    public string SelectedCountText =>
        string.Format(CultureInfo.CurrentCulture, AppResources.ImageModelsSelectedFormat, _all.Count(m => m.IsSelected));

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
            var knownRefs = _all.Select(m => m.Reference).ToHashSet();
            // Models that are saved but not in the catalog stay selected: the catalog may not list them.
            var unknownSelected = session.Settings.ImageModels.Where(reference => !knownRefs.Contains(reference));
            var settings = new SessionSettings
            {
                ImageModels = _all.Where(m => m.IsSelected).Select(m => m.Reference).Concat(unknownSelected).ToList(),
                RunsPerModel = (int)Math.Round(RunsPerModel),
                // Without a row chosen (the list is empty or failed to load) the saved assistant stays.
                AssistantModel = _assistants.FirstOrDefault(a => a.IsSelected)?.Reference ?? session.Settings.AssistantModel,
            };
            await sessionService.UpdateSettingsAsync(_sessionId, settings, CancellationToken.None);
            await CloseAsync();
        }
        catch (Exception ex)
        {
            await _errorAlert.ShowAsync(ex);
        }
    }

    /// <summary>Leaves the screen without saving: the session keeps its settings.</summary>
    [RelayCommand]
    private Task CancelAsync() => CloseAsync();

    /// <summary>In pane mode raises <see cref="CloseRequested"/>; otherwise goes back, as before.</summary>
    private Task CloseAsync()
    {
        if (HostedInPane)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        return navigation.GoBackAsync();
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

            // After the first load the checkboxes and the chosen row are the source of truth (they may hold unsaved changes).
            var selected = _loaded
                ? _all.Where(m => m.IsSelected).Select(m => m.Reference).ToHashSet()
                : session.Settings.ImageModels.ToHashSet();
            var assistant = _loaded
                ? _assistants.FirstOrDefault(a => a.IsSelected)?.Reference
                : session.Settings.AssistantModel;
            if (!_loaded)
            {
                RunsPerModel = session.Settings.RunsPerModel;
            }

            var models = await catalog.GetAllImageModelsAsync(forceRefresh, CancellationToken.None);
            _all.Clear();
            _all.AddRange(models.Select(m => new ModelOptionViewModel(
                m,
                providers.Get(m.ProviderId).DisplayName,
                selected.Contains(new ModelRef(m.ProviderId, m.Id)))));
            foreach (var row in _all)
            {
                row.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(ModelOptionViewModel.IsSelected))
                    {
                        OnPropertyChanged(nameof(SelectedCountText));
                        foreach (var group in ImageModelGroups)
                        {
                            group.NotifySelectionChanged();
                        }
                    }
                };
            }
            OnPropertyChanged(nameof(SelectedCountText));

            var assistants = await chatModels.ListAllAssistantModelsAsync(CancellationToken.None);
            _assistants.Clear();
            _assistants.AddRange(assistants.Select(item =>
            {
                var reference = new ModelRef(item.ProviderId, item.Model.Id);
                var providerText = $"{providers.Get(item.ProviderId).DisplayName} ({item.ProviderId})";
                return new AssistantModelOptionViewModel(reference, providerText, item.Model, reference == assistant, SelectAssistant);
            }));

            _loaded = true;
            ApplyFilter();
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

    /// <summary>One assistant model at a time: the chosen row is selected, the others are cleared.</summary>
    private void SelectAssistant(AssistantModelOptionViewModel chosen)
    {
        foreach (var row in _assistants)
        {
            row.IsSelected = row == chosen;
        }
    }

    private void ApplyFilter()
    {
        var search = SearchText.Trim();
        Models.Clear();
        foreach (var model in _all.Where(m => Matches(search, m.Name, m.Id)))
        {
            Models.Add(model);
        }

        AssistantModels.Clear();
        foreach (var model in _assistants.Where(m => Matches(search, m.Name, m.Reference.ModelId)))
        {
            AssistantModels.Add(model);
        }

        RebuildImageModelGroups(search);
        RebuildAssistantModelGroups(search);
    }

    private void RebuildImageModelGroups(string search)
    {
        ImageModelGroups.Clear();
        foreach (var group in ModelVendors.Group(Models, model => model.Reference.ProviderId, model => model.Reference.ModelId, model => model.Name))
        {
            if (!_imageGroupExpanded.TryGetValue(group.Key, out var isExpanded))
            {
                isExpanded = group.Items.Any(model => model.IsSelected);
                _imageGroupExpanded[group.Key] = isExpanded;
            }

            if (search.Length > 0)
            {
                isExpanded = true;
            }

            ImageModelGroups.Add(new ImageModelGroupViewModel(group.Key, group.Label, group.Items, isExpanded, SaveImageGroupExpansion));
        }
    }

    private void RebuildAssistantModelGroups(string search)
    {
        AssistantModelGroups.Clear();
        foreach (var group in ModelVendors.Group(
                     AssistantModels,
                     model => model.Reference.ProviderId,
                     model => model.Reference.ModelId,
                     model => model.Name))
        {
            if (!_assistantGroupExpanded.TryGetValue(group.Key, out var isExpanded))
            {
                isExpanded = group.Items.Any(model => model.IsSelected);
                _assistantGroupExpanded[group.Key] = isExpanded;
            }

            if (search.Length > 0)
            {
                isExpanded = true;
            }

            AssistantModelGroups.Add(new AssistantModelGroupViewModel(group.Key, group.Label, group.Items, isExpanded, SaveAssistantGroupExpansion));
        }
    }

    private void SaveImageGroupExpansion(ModelGroupViewModel group)
    {
        if (SearchText.Trim().Length == 0)
        {
            _imageGroupExpanded[group.Key] = group.IsExpanded;
        }
    }

    private void SaveAssistantGroupExpansion(ModelGroupViewModel group)
    {
        if (SearchText.Trim().Length == 0)
        {
            _assistantGroupExpanded[group.Key] = group.IsExpanded;
        }
    }

    private static bool Matches(string search, string name, string id) =>
        search.Length == 0
        || name.Contains(search, StringComparison.OrdinalIgnoreCase)
        || id.Contains(search, StringComparison.OrdinalIgnoreCase);
}

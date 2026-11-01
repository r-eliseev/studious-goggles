using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.PageModels;

/// <summary>
/// Picks a model to run one generation on (task 076). The quick list on top holds the image models already used in
/// this session (except the card's own model), ordered by last use. The "All models" block holds the whole catalog
/// with a search that filters it only. Picking a row starts a run (same prompt and seed, the chosen model) and goes back.
/// The page forwards the Shell query to <see cref="ApplyQueryAttributes"/> and calls <see cref="OnAppearingAsync"/>.
/// </summary>
public sealed partial class ModelPickerPageModel(
    IRunRepository runs,
    ModelCatalogService catalog,
    GenerationService generations,
    ProviderRegistry providers,
    INavigationService navigation,
    ErrorAlert errorAlert) : ObservableObject
{
    private readonly ErrorAlert _errorAlert = errorAlert;
    private readonly List<ModelOptionViewModel> _all = [];
    private Guid _sessionId;
    private Guid _generationId;

    /// <summary>Image models already used in this session (except the card's model), most recent first.</summary>
    public ObservableCollection<ModelOptionViewModel> UsedModels { get; } = [];

    /// <summary>The whole catalog filtered by <see cref="SearchText"/>.</summary>
    public ObservableCollection<ModelOptionViewModel> AllModels { get; } = [];

    /// <summary>The "All models" block is collapsed by default; the user expands it.</summary>
    [ObservableProperty]
    public partial bool IsAllExpanded { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Reads the session id and the generation id from the route query. The page forwards the Shell query here.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!query.TryGetValue(Routes.SessionIdParameter, out var sessionValue)
            || sessionValue is not string sessionText
            || !Guid.TryParse(sessionText, out var sessionId))
        {
            throw new ArgumentException(
                $"The model picker route needs a valid '{Routes.SessionIdParameter}' query parameter.", nameof(query));
        }

        if (!query.TryGetValue(Routes.GenerationIdParameter, out var generationValue)
            || generationValue is not string generationText
            || !Guid.TryParse(generationText, out var generationId))
        {
            throw new ArgumentException(
                $"The model picker route needs a valid '{Routes.GenerationIdParameter}' query parameter.", nameof(query));
        }

        _sessionId = sessionId;
        _generationId = generationId;
    }

    public Task OnAppearingAsync() => LoadAsync();

    /// <summary>Expands or collapses the "All models" block (the shells that have no expander control use this).</summary>
    [RelayCommand]
    private void ToggleAll() => IsAllExpanded = !IsAllExpanded;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var source = await runs.GetGenerationAsync(_generationId, CancellationToken.None)
                ?? throw new InvalidOperationException($"Generation {_generationId} not found.");
            var sourceModel = new ModelRef(source.ProviderId, source.ModelId);

            var models = await catalog.GetAllImageModelsAsync(forceRefresh: false, CancellationToken.None);
            var byReference = models.ToDictionary(m => new ModelRef(m.ProviderId, m.Id));
            _all.Clear();
            _all.AddRange(models.Select(m => new ModelOptionViewModel(m, providers.Get(m.ProviderId).DisplayName, isSelected: false)));

            var sessionGenerations = await runs.ListGenerationsBySessionAsync(_sessionId, CancellationToken.None);
            // Most recent use last in the list: later entries overwrite the order index, then we reverse.
            var lastUse = new Dictionary<ModelRef, int>();
            var index = 0;
            foreach (var generation in sessionGenerations)
            {
                lastUse[new ModelRef(generation.ProviderId, generation.ModelId)] = index++;
            }

            UsedModels.Clear();
            var used = lastUse
                .Where(pair => pair.Key != sourceModel && byReference.ContainsKey(pair.Key))
                .OrderByDescending(pair => pair.Value)
                .Select(pair => new ModelOptionViewModel(byReference[pair.Key], providers.Get(pair.Key.ProviderId).DisplayName, isSelected: false));
            foreach (var row in used)
            {
                UsedModels.Add(row);
            }

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

    [RelayCommand]
    private async Task PickAsync(ModelOptionViewModel? model)
    {
        if (model is null)
        {
            return;
        }

        try
        {
            await generations.RunOnModelAsync(_generationId, model.Reference, CancellationToken.None);
            await navigation.GoBackAsync();
        }
        catch (Exception ex)
        {
            await _errorAlert.ShowAsync(ex);
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var search = SearchText.Trim();
        AllModels.Clear();
        foreach (var model in _all.Where(m => Matches(search, m.Name, m.Id)))
        {
            AllModels.Add(model);
        }
    }

    private static bool Matches(string search, string name, string id) =>
        search.Length == 0
        || name.Contains(search, StringComparison.OrdinalIgnoreCase)
        || id.Contains(search, StringComparison.OrdinalIgnoreCase);
}

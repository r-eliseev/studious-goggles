using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Services;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.PageModels;

/// <summary>
/// Parameters of one session (F4.2): aspect ratio, resolution, output format and quality. The choices come from the
/// catalog of the models the session uses; a saved value that no model lists is still offered, so it can be kept.
/// The page forwards the Shell query to <see cref="ApplyQueryAttributes"/> and calls <see cref="OnAppearingAsync"/>.
/// </summary>
public sealed partial class SessionParametersPageModel(
    SessionService sessionService,
    ModelCatalogService catalog,
    INavigationService navigation,
    IDialogService dialogs) : ObservableObject
{
    /// <summary>Output formats the service accepts (see <c>SessionService</c>).</summary>
    private static readonly string[] OutputFormats = ["png", "jpeg", "webp"];

    private Guid _sessionId;

    public ObservableCollection<ParameterOption> AspectRatioOptions { get; } = [];

    public ObservableCollection<ParameterOption> ResolutionOptions { get; } = [];

    public ObservableCollection<ParameterOption> OutputFormatOptions { get; } = [];

    public ObservableCollection<ParameterOption> QualityOptions { get; } = [];

    [ObservableProperty]
    public partial ParameterOption? SelectedAspectRatio { get; set; }

    [ObservableProperty]
    public partial ParameterOption? SelectedResolution { get; set; }

    [ObservableProperty]
    public partial ParameterOption? SelectedOutputFormat { get; set; }

    [ObservableProperty]
    public partial ParameterOption? SelectedQuality { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>Reads the session id from the route query.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!query.TryGetValue(Routes.SessionIdParameter, out var value)
            || value is not string text
            || !Guid.TryParse(text, out var sessionId))
        {
            throw new ArgumentException(
                $"The parameters route needs a valid '{Routes.SessionIdParameter}' query parameter.", nameof(query));
        }

        _sessionId = sessionId;
    }

    public Task OnAppearingAsync() => LoadAsync();

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task SaveAsync()
    {
        try
        {
            var parameters = new GenerationParameters
            {
                AspectRatio = SelectedAspectRatio?.Value,
                Resolution = SelectedResolution?.Value,
                OutputFormat = SelectedOutputFormat?.Value,
                Quality = SelectedQuality?.Value,
            };
            await sessionService.UpdateGenerationParametersAsync(_sessionId, parameters, CancellationToken.None);
            await navigation.GoBackAsync();
        }
        catch (Exception ex)
        {
            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
        }
    }

    private bool CanRun() => !IsBusy;

    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var session = await sessionService.GetAsync(_sessionId, CancellationToken.None)
                ?? throw new InvalidOperationException($"Session {_sessionId} not found.");
            var saved = session.Settings.Parameters;
            var selected = session.Settings.ImageModels.ToHashSet();
            var models = (await catalog.GetAllImageModelsAsync(forceRefresh: false, CancellationToken.None))
                .Where(m => selected.Contains(new ModelRef(m.ProviderId, m.Id)))
                .ToList();

            SelectedAspectRatio = FillWith(AspectRatioOptions, CatalogValues(models, GenerationParameterPlanner.AspectRatioName), saved.AspectRatio);
            SelectedResolution = FillWith(ResolutionOptions, CatalogValues(models, GenerationParameterPlanner.ResolutionName), saved.Resolution);
            SelectedOutputFormat = FillWith(OutputFormatOptions, OutputFormats, saved.OutputFormat);
            SelectedQuality = FillWith(QualityOptions, CatalogValues(models, GenerationParameterPlanner.QualityName), saved.Quality);
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

    /// <summary>Values that the selected models declare for the parameter (enum kind only), without repeats.</summary>
    private static IEnumerable<string> CatalogValues(IEnumerable<ImageModel> models, string parameterName) =>
        models.SelectMany(m => m.Parameters ?? Array.Empty<ModelParameter>())
            .Where(p => p.Kind == ParameterKind.Enum && string.Equals(p.Name, parameterName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(p => p.Values);

    private static ParameterOption FillWith(ObservableCollection<ParameterOption> target, IEnumerable<string> values, string? savedValue)
    {
        var distinct = values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (savedValue is not null && !distinct.Contains(savedValue, StringComparer.OrdinalIgnoreCase))
        {
            distinct.Add(savedValue);
        }

        target.Clear();
        var defaultOption = new ParameterOption(null, AppResources.ParameterModelDefault);
        target.Add(defaultOption);
        foreach (var value in distinct)
        {
            target.Add(new ParameterOption(value, value));
        }

        return savedValue is null
            ? defaultOption
            : target.FirstOrDefault(o => string.Equals(o.Value, savedValue, StringComparison.OrdinalIgnoreCase)) ?? defaultOption;
    }
}

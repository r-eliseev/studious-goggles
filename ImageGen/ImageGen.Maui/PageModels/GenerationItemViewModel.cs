using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Storage;
using ImageGen.Maui.Resources.Strings;

namespace ImageGen.Maui.PageModels;

/// <summary>
/// One generation card of a run. <see cref="Update"/> refreshes it from a saved generation and must run on the UI thread.
/// The commands call the functions of the owning page model. The image is exposed as a file path; the view converts it.
/// </summary>
public sealed partial class GenerationItemViewModel : ObservableObject
{
    private readonly IImageFileStore _files;
    private readonly Func<GenerationItemViewModel, Task> _repeat;
    private readonly Func<GenerationItemViewModel, Task> _newSeed;
    private readonly Func<GenerationItemViewModel, Task> _edit;

    public GenerationItemViewModel(
        Generation generation,
        IImageFileStore files,
        Func<GenerationItemViewModel, Task> repeat,
        Func<GenerationItemViewModel, Task> newSeed,
        Func<GenerationItemViewModel, Task> edit)
    {
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(repeat);
        ArgumentNullException.ThrowIfNull(newSeed);
        ArgumentNullException.ThrowIfNull(edit);
        _files = files;
        _repeat = repeat;
        _newSeed = newSeed;
        _edit = edit;
        Id = generation.Id;
        Prompt = generation.Prompt;
        Update(generation);
    }

    public Guid Id { get; }

    /// <summary>Exact prompt sent to the model.</summary>
    public string Prompt { get; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    /// <summary>Model, seed (or "seed not supported") and price when known.</summary>
    [ObservableProperty]
    public partial string DetailsText { get; set; } = "";

    [ObservableProperty]
    public partial string ErrorText { get; set; } = "";

    /// <summary>Status, details and error in one line for screen readers.</summary>
    [ObservableProperty]
    public partial string SummaryText { get; set; } = "";

    /// <summary>Absolute path of the saved image, or empty when there is no image.</summary>
    [ObservableProperty]
    public partial string ImageFilePath { get; set; } = "";

    [ObservableProperty]
    public partial bool HasImage { get; set; }

    /// <summary>True when <see cref="ErrorText"/> is not empty (failures and provider filtering both show a reason).</summary>
    [ObservableProperty]
    public partial bool HasErrorText { get; set; }

    /// <summary>True only for a failed generation with an error text. A filtered generation is not an error.</summary>
    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RepeatCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewSeedCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    public partial bool IsFinished { get; set; }

    [RelayCommand(CanExecute = nameof(IsFinished))]
    private Task RepeatAsync() => _repeat(this);

    [RelayCommand(CanExecute = nameof(IsFinished))]
    private Task NewSeedAsync() => _newSeed(this);

    [RelayCommand(CanExecute = nameof(IsFinished))]
    private Task EditAsync() => _edit(this);

    /// <summary>Recomputes every displayed value from the saved generation.</summary>
    public void Update(Generation generation)
    {
        ArgumentNullException.ThrowIfNull(generation);
        if (generation.Id != Id)
        {
            throw new ArgumentException("The generation does not belong to this card.", nameof(generation));
        }

        StatusText = StatusTextOf(generation.Status);
        DetailsText = DetailsOf(generation);
        ErrorText = generation.ErrorMessage?.Trim() ?? "";
        HasErrorText = ErrorText.Length > 0;
        HasError = generation.Status == GenerationStatus.Failed && HasErrorText;
        HasImage = generation.Status == GenerationStatus.Succeeded && !string.IsNullOrEmpty(generation.ImagePath);
        ImageFilePath = HasImage ? _files.GetFullPath(generation.ImagePath!) : "";
        IsRunning = generation.Status is GenerationStatus.Queued or GenerationStatus.Running;
        IsFinished = generation.IsFinished;
        SummaryText = string.Join(". ", new[] { StatusText, DetailsText, ErrorText }.Where(text => text.Length > 0));
    }

    private static string StatusTextOf(GenerationStatus status) => status switch
    {
        GenerationStatus.Queued => AppResources.StatusQueued,
        GenerationStatus.Running => AppResources.StatusRunning,
        GenerationStatus.Succeeded => AppResources.StatusSucceeded,
        GenerationStatus.Failed => AppResources.StatusFailed,
        GenerationStatus.Cancelled => AppResources.StatusCancelled,
        GenerationStatus.Filtered => AppResources.StatusFiltered,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private static string DetailsOf(Generation generation)
    {
        var parts = new List<string>
        {
            generation.ModelId,
            generation.SeedApplied
                ? string.Format(CultureInfo.InvariantCulture, AppResources.SeedFormat, generation.Seed)
                : AppResources.SeedNotSupported,
        };

        if (generation.CostUsd is { } cost)
        {
            var price = cost.ToString("0.####", CultureInfo.InvariantCulture);
            parts.Add(string.Format(CultureInfo.InvariantCulture, AppResources.CostFormat, price));
        }

        return string.Join(" · ", parts);
    }
}

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Descriptions;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.PageModels;

/// <summary>
/// One generation card of a run. <see cref="Update"/> refreshes it from a saved generation and must run on the UI thread.
/// The commands call the functions of the owning page model. The image is exposed as a file path; the view converts it.
/// </summary>
public sealed partial class GenerationItemViewModel : ObservableObject
{
    private readonly IImageFileStore _files;
    private readonly IDialogService _dialogs;
    private readonly Func<GenerationItemViewModel, Task> _repeat;
    private readonly Func<GenerationItemViewModel, Task> _newSeed;
    private readonly Func<GenerationItemViewModel, Task> _edit;
    private readonly Func<GenerationItemViewModel, DescriptionMode, Task> _describe;

    public GenerationItemViewModel(
        Generation generation,
        IImageFileStore files,
        IDialogService dialogs,
        Func<GenerationItemViewModel, Task> repeat,
        Func<GenerationItemViewModel, Task> newSeed,
        Func<GenerationItemViewModel, Task> edit,
        Func<GenerationItemViewModel, DescriptionMode, Task> describe)
    {
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(repeat);
        ArgumentNullException.ThrowIfNull(newSeed);
        ArgumentNullException.ThrowIfNull(edit);
        _files = files;
        _dialogs = dialogs;
        _repeat = repeat;
        _newSeed = newSeed;
        _edit = edit;
        _describe = describe;
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

    /// <summary>Model id of the generation.</summary>
    [ObservableProperty]
    public partial string ModelText { get; set; } = "";

    /// <summary>Seed ("seed N") or "seed not supported".</summary>
    [ObservableProperty]
    public partial string SeedText { get; set; } = "";

    /// <summary>Price ("$N") when known, otherwise empty.</summary>
    [ObservableProperty]
    public partial string CostText { get; set; } = "";

    /// <summary>Generation time ("Ns") when both timestamps are known, otherwise empty.</summary>
    [ObservableProperty]
    public partial string TimeText { get; set; } = "";

    /// <summary>Parameter lines (replaced or dropped) and the filtered explanation; empty when none.</summary>
    [ObservableProperty]
    public partial string NoticeText { get; set; } = "";

    /// <summary>True when <see cref="NoticeText"/> is not empty.</summary>
    [ObservableProperty]
    public partial bool HasNotice { get; set; }

    /// <summary>Status of the card: one placeholder and one status label per value.</summary>
    [ObservableProperty]
    public partial GenerationStatusKind StatusKind { get; set; }

    [ObservableProperty]
    public partial bool IsDone { get; set; }

    [ObservableProperty]
    public partial bool IsQueued { get; set; }

    /// <summary>True only while the generation is running (not queued), for the status badge class.</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    public partial bool IsFailed { get; set; }

    [ObservableProperty]
    public partial bool IsFiltered { get; set; }

    [ObservableProperty]
    public partial bool IsCancelled { get; set; }

    /// <summary>Price in USD when known; used by the run header to total the cost.</summary>
    public decimal? CostUsd { get; private set; }

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

    /// <summary>True for a failed or filtered generation: the Repeat action stays visible even without hover (task 069).</summary>
    [ObservableProperty]
    public partial bool IsFailedOrFiltered { get; set; }

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RepeatCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewSeedCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DescribeCommand))]
    [NotifyCanExecuteChangedFor(nameof(DescribePromptCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShowActionsCommand))]
    public partial bool IsFinished { get; set; }

    [RelayCommand(CanExecute = nameof(IsFinished))]
    private Task RepeatAsync() => _repeat(this);

    [RelayCommand(CanExecute = nameof(IsFinished))]
    private Task NewSeedAsync() => _newSeed(this);

    [RelayCommand(CanExecute = nameof(IsFinished))]
    private Task EditAsync() => _edit(this);

    /// <summary>Describes this picture (task 019): the text is shown, and it goes into the input field only on the user's action.</summary>
    [RelayCommand(CanExecute = nameof(IsFinished))]
    private Task DescribeAsync() => _describe(this, DescriptionMode.Description);

    [RelayCommand(CanExecute = nameof(IsFinished))]
    private Task DescribePromptAsync() => _describe(this, DescriptionMode.Prompt);

    /// <summary>
    /// Opens the actions menu (phone action sheet or right-click/"More" context menu). The title is the card meta line
    /// (model, seed, cost, time), the options are the same actions as the icon row, and the chosen one runs.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsFinished))]
    private async Task ShowActionsAsync()
    {
        var actions = new (string Label, Func<GenerationItemViewModel, Task> Run)[]
        {
            (AppResources.Repeat, _repeat),
            (AppResources.NewSeed, _newSeed),
            (AppResources.ActionEditPrompt, _edit),
            (AppResources.ActionDescribeImage, card => _describe(card, DescriptionMode.Description)),
            (AppResources.ActionPromptFromImage, card => _describe(card, DescriptionMode.Prompt)),
        };

        var title = string.Join(" · ", new[] { ModelText, SeedText, CostText, TimeText }.Where(text => text.Length > 0));
        var chosen = await _dialogs.ChooseAsync(title, AppResources.Cancel, actions.Select(action => action.Label).ToList());
        if (chosen is null)
        {
            return;
        }

        foreach (var action in actions)
        {
            if (string.Equals(action.Label, chosen, StringComparison.Ordinal))
            {
                await action.Run(this);
                return;
            }
        }
    }

    /// <summary>Recomputes every displayed value from the saved generation.</summary>
    public void Update(Generation generation)
    {
        ArgumentNullException.ThrowIfNull(generation);
        if (generation.Id != Id)
        {
            throw new ArgumentException("The generation does not belong to this card.", nameof(generation));
        }

        StatusText = StatusTextOf(generation.Status);
        StatusKind = StatusKindOf(generation.Status);
        DetailsText = DetailsOf(generation);
        ModelText = generation.ModelId;
        SeedText = SeedTextOf(generation);
        CostText = CostTextOf(generation);
        TimeText = TimeTextOf(generation);
        NoticeText = NoticeOf(generation);
        HasNotice = NoticeText.Length > 0;
        CostUsd = generation.CostUsd;
        IsDone = generation.Status == GenerationStatus.Succeeded;
        IsQueued = generation.Status == GenerationStatus.Queued;
        IsActive = generation.Status == GenerationStatus.Running;
        IsFailed = generation.Status == GenerationStatus.Failed;
        IsFiltered = generation.Status == GenerationStatus.Filtered;
        IsFailedOrFiltered = IsFailed || IsFiltered;
        IsCancelled = generation.Status == GenerationStatus.Cancelled;
        ErrorText = generation.ErrorMessage?.Trim() ?? "";
        HasErrorText = ErrorText.Length > 0;
        HasError = generation.Status == GenerationStatus.Failed && HasErrorText;
        HasImage = generation.Status == GenerationStatus.Succeeded && !string.IsNullOrEmpty(generation.ImagePath);
        ImageFilePath = HasImage ? _files.GetFullPath(generation.ImagePath!) : "";
        IsRunning = generation.Status is GenerationStatus.Queued or GenerationStatus.Running;
        IsFinished = generation.IsFinished;
        SummaryText = string.Join(". ", new[] { StatusText, DetailsText, ErrorText }.Where(text => text.Length > 0));
    }

    private static GenerationStatusKind StatusKindOf(GenerationStatus status) => status switch
    {
        GenerationStatus.Queued => GenerationStatusKind.Queued,
        GenerationStatus.Running => GenerationStatusKind.Running,
        GenerationStatus.Succeeded => GenerationStatusKind.Done,
        GenerationStatus.Failed => GenerationStatusKind.Failed,
        GenerationStatus.Cancelled => GenerationStatusKind.Cancelled,
        GenerationStatus.Filtered => GenerationStatusKind.Filtered,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private static string SeedTextOf(Generation generation) => generation.SeedApplied
        ? string.Format(CultureInfo.InvariantCulture, AppResources.SeedFormat, generation.Seed)
        : AppResources.SeedNotSupported;

    private static string CostTextOf(Generation generation)
    {
        if (generation.CostUsd is not { } cost)
        {
            return "";
        }

        var price = cost.ToString("0.####", CultureInfo.InvariantCulture);
        return string.Format(CultureInfo.InvariantCulture, AppResources.CostFormat, price);
    }

    private static string TimeTextOf(Generation generation)
    {
        if (generation.CompletedAt is not { } completed)
        {
            return "";
        }

        var seconds = (completed - generation.CreatedAt).TotalSeconds;
        if (seconds <= 0)
        {
            return "";
        }

        var value = seconds.ToString("0.#", CultureInfo.InvariantCulture);
        return string.Format(CultureInfo.InvariantCulture, AppResources.TimeFormat, value);
    }

    private static string NoticeOf(Generation generation)
    {
        var lines = new List<string>();
        if (generation.Status == GenerationStatus.Filtered)
        {
            lines.Add(AppResources.FilteredNotice);
        }

        lines.AddRange(ParameterLines(generation.Outcomes));
        return string.Join("\n", lines);
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

        if (generation.ReferenceAttachmentIds.Count > 0)
        {
            parts.Add(string.Format(CultureInfo.CurrentCulture, AppResources.ReferenceFormat, generation.ReferenceAttachmentIds.Count));
        }

        parts.AddRange(ParameterLines(generation.Outcomes));
        return string.Join(" · ", parts);
    }

    /// <summary>One line per replaced or dropped parameter (F4.3). An applied parameter needs no line.</summary>
    private static IEnumerable<string> ParameterLines(IEnumerable<ParameterOutcome> outcomes)
    {
        foreach (var outcome in outcomes)
        {
            var label = ParameterLabel(outcome.Name);
            switch (outcome.Kind)
            {
                case ParameterOutcomeKind.Replaced:
                    yield return string.Format(CultureInfo.CurrentCulture, AppResources.ParameterReplacedFormat, label, outcome.Requested, outcome.Sent);
                    break;
                case ParameterOutcomeKind.Dropped:
                    yield return string.Format(
                        CultureInfo.CurrentCulture,
                        AppResources.ParameterDroppedFormat,
                        label,
                        outcome.Requested,
                        ReasonText(outcome.Reason));
                    break;
            }
        }
    }

    private static string ParameterLabel(string name) => name switch
    {
        GenerationParameterPlanner.AspectRatioName => AppResources.ParameterAspectRatio,
        GenerationParameterPlanner.ResolutionName => AppResources.ParameterResolution,
        GenerationParameterPlanner.OutputFormatName => AppResources.ParameterOutputFormat,
        GenerationParameterPlanner.QualityName => AppResources.ParameterQuality,
        _ => name,
    };

    private static string ReasonText(ParameterDropReason? reason) => reason switch
    {
        ParameterDropReason.NotSupported => AppResources.ReasonNotSupported,
        ParameterDropReason.NoAllowedValue => AppResources.ReasonNoAllowedValue,
        ParameterDropReason.CatalogUnknown => AppResources.ReasonCatalogUnknown,
        _ => string.Empty,
    };
}

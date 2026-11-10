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
    private readonly IClipboardService _clipboard;
    private readonly IImageSaver _saver;
    private readonly IMediaPlayerService? _mediaPlayer;
    private readonly IVideoPosterService? _videoPosterService;
    private readonly string? _videoAspectRatio;
    private readonly object _videoPosterLock = new();
    private Task<PosterInfo?>? _videoPosterRead;
    private readonly Func<GenerationItemViewModel, Task> _repeat;
    private readonly Func<GenerationItemViewModel, Task> _newSeed;
    private readonly Func<GenerationItemViewModel, Task> _edit;
    private readonly Func<GenerationItemViewModel, DescriptionMode, Task> _describe;
    private readonly Func<GenerationItemViewModel, Task> _otherModel;
    private readonly Func<GenerationItemViewModel, Task>? _compare;
    private readonly Func<GenerationItemViewModel, Task>? _cancel;
    private string _mediaType = "image/png";
    private string _generationImagePath = "";
    private bool _stoppedWaiting;

    public GenerationItemViewModel(
        Generation generation,
        IImageFileStore files,
        IDialogService dialogs,
        IClipboardService clipboard,
        IImageSaver saver,
        Func<GenerationItemViewModel, Task> repeat,
        Func<GenerationItemViewModel, Task> newSeed,
        Func<GenerationItemViewModel, Task> edit,
        Func<GenerationItemViewModel, DescriptionMode, Task> describe,
        Func<GenerationItemViewModel, Task> otherModel,
        Func<GenerationItemViewModel, Task>? compare = null,
        Func<GenerationItemViewModel, Task>? cancel = null,
        IMediaPlayerService? mediaPlayer = null,
        IVideoPosterService? videoPosterService = null,
        string? videoAspectRatio = null)
    {
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(saver);
        ArgumentNullException.ThrowIfNull(repeat);
        ArgumentNullException.ThrowIfNull(newSeed);
        ArgumentNullException.ThrowIfNull(edit);
        ArgumentNullException.ThrowIfNull(otherModel);
        _files = files;
        _dialogs = dialogs;
        _clipboard = clipboard;
        _saver = saver;
        _mediaPlayer = mediaPlayer;
        _videoPosterService = videoPosterService;
        _videoAspectRatio = videoAspectRatio;
        _repeat = repeat;
        _newSeed = newSeed;
        _edit = edit;
        _describe = describe;
        _otherModel = otherModel;
        _compare = compare;
        _cancel = cancel;
        Id = generation.Id;
        ProviderId = generation.ProviderId;
        ModelId = generation.ModelId;
        Prompt = generation.Prompt;
        Update(generation);
    }

    public Guid Id { get; }

    public string ProviderId { get; }

    public string ModelId { get; }

    /// <summary>Exact prompt sent to the model.</summary>
    public string Prompt { get; }

    /// <summary>Relative path used by the image store, or empty when the generation has no image.</summary>
    public string RelativeImagePath => _generationImagePath;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [ObservableProperty]
    public partial string CancellationStatusText { get; set; } = "";

    [ObservableProperty]
    public partial bool HasCancellationStatus { get; set; }

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
    [NotifyCanExecuteChangedFor(nameof(DetailsCommand))]
    public partial bool IsFailed { get; set; }

    [ObservableProperty]
    public partial bool IsFiltered { get; set; }

    [ObservableProperty]
    public partial bool IsCancelled { get; set; }

    private decimal? _costUsd;

    /// <summary>Price in USD when known; used by the run header to total the cost.</summary>
    public decimal? CostUsd
    {
        get => _costUsd;
        private set => SetProperty(ref _costUsd, value);
    }

    [ObservableProperty]
    public partial string ErrorText { get; set; } = "";

    /// <summary>The provider's own error text, shown only in the details dialog.</summary>
    public string TechnicalError { get; private set; } = "";

    /// <summary>Status, details and error in one line for screen readers.</summary>
    [ObservableProperty]
    public partial string SummaryText { get; set; } = "";

    [ObservableProperty]
    public partial string ImageDimensionsText { get; set; } = "";

    [ObservableProperty]
    public partial double ImageAreaHeight { get; set; } = 176;

    [ObservableProperty]
    public partial bool IsVideo { get; set; }

    [ObservableProperty]
    public partial bool IsImage { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NewSeedCommand))]
    [NotifyPropertyChangedFor(nameof(CanShowNewSeed))]
    public partial bool IsAudio { get; set; }

    public bool CanShowNewSeed => !IsAudio;

    [ObservableProperty]
    public partial string MediaFilePath { get; set; } = "";

    [ObservableProperty]
    public partial string VideoFilePath { get; set; } = "";

    [ObservableProperty]
    public partial string VideoPosterPath { get; set; } = "";

    [ObservableProperty]
    public partial string AudioFilePath { get; set; } = "";

    [ObservableProperty]
    public partial string DurationText { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool HasMedia { get; set; }

    [ObservableProperty]
    public partial bool HasImageDimensions { get; set; }

    /// <summary>Absolute path of the saved image, or empty when there is no image.</summary>
    [ObservableProperty]
    public partial string ImageFilePath { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompareCommand))]
    public partial bool HasImage { get; set; }

    /// <summary>Caption of the save action in this shell (task 080).</summary>
    public string SaveLabel => IsVideo || IsAudio ? AppResources.SaveFile : _saver.ActionLabel;

    public bool CanPlayMedia => _mediaPlayer?.IsSupported == true && HasMedia && (IsVideo || IsAudio);

    public async Task<PosterInfo?> GetVideoPosterAsync(CancellationToken cancellationToken)
    {
        if (_videoPosterService?.IsSupported != true || !IsVideo || !HasMedia || string.IsNullOrWhiteSpace(VideoFilePath))
        {
            return null;
        }

        Task<PosterInfo?> read;
        lock (_videoPosterLock)
        {
            _videoPosterRead ??= _videoPosterService.GetPosterAsync(VideoFilePath, cancellationToken);
            read = _videoPosterRead;
        }

        return await read;
    }

    public void ApplyVideoPoster(PosterInfo? poster)
    {
        VideoPosterPath = poster?.Path ?? "";
        ImageAreaHeight = poster is { Width: > 0, Height: > 0 }
            ? CardImageLayout.HeightFor(poster.Width, poster.Height)
            : VideoHeightForAspectRatio(_videoAspectRatio);
    }

    /// <summary>«Saved» after a successful save; empty otherwise. Shown on the card as a status line.</summary>
    [ObservableProperty]
    public partial string SaveStatusText { get; set; } = "";

    /// <summary>True when <see cref="ErrorText"/> is not empty (failures and provider filtering both show a reason).</summary>
    [ObservableProperty]
    public partial bool HasErrorText { get; set; }

    /// <summary>True only for a failed generation with an error text. A filtered generation is not an error.</summary>
    [ObservableProperty]
    public partial bool HasError { get; set; }

    /// <summary>True for a failed or filtered generation: the Repeat action stays visible even without hover (task 069).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OtherModelCommand))]
    public partial bool IsFailedOrFiltered { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    public partial bool IsCancelRequested { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RepeatCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewSeedCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DescribeCommand))]
    [NotifyCanExecuteChangedFor(nameof(DescribePromptCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyPromptCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShowActionsCommand))]
    public partial bool IsFinished { get; set; }

    [RelayCommand(CanExecute = nameof(IsFinished))]
    private Task RepeatAsync() => _repeat(this);

    [RelayCommand(CanExecute = nameof(CanNewSeed))]
    private Task NewSeedAsync() => _newSeed(this);

    private bool CanNewSeed() => IsFinished && !IsAudio;

    [RelayCommand(CanExecute = nameof(IsFinished))]
    private Task EditAsync() => _edit(this);

    /// <summary>Saves the picture through the shell's saver (task 080). A refusal or a failure shows a message; a cancelled dialog does nothing.</summary>
    [RelayCommand(CanExecute = nameof(HasMedia))]
    private async Task SaveAsync()
    {
        var result = await _saver.SaveAsync(MediaFilePath, ImageFileNames.Suggested(Id, _mediaType), _mediaType, CancellationToken.None);
        switch (result)
        {
            case ImageSaveResult.Saved:
                SaveStatusText = AppResources.Saved;
                break;
            case ImageSaveResult.PermissionDenied:
                await _dialogs.AlertAsync(AppResources.ErrorTitle, AppResources.SavePermissionDenied);
                break;
            case ImageSaveResult.Failed:
                await _dialogs.AlertAsync(AppResources.ErrorTitle, AppResources.SaveFailed);
                break;
        }
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private Task CompareAsync() => _compare?.Invoke(this) ?? Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanPlayMedia))]
    private Task PlayMediaAsync() => _mediaPlayer?.ShowAsync(
        MediaFilePath, IsAudio ? AppResources.PlayAudio : AppResources.PlayVideo, CancellationToken.None) ?? Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private Task CancelAsync() => _cancel?.Invoke(this) ?? Task.CompletedTask;

    private bool CanCancel() => IsRunning && !IsCancelRequested;

    public void SetCancellationFailed()
    {
        CancellationStatusText = AppResources.CancelFailed;
        HasCancellationStatus = true;
    }

    public void SetStoppedWaiting()
    {
        _stoppedWaiting = true;
        CancellationStatusText = AppResources.StoppedWaitingMedia;
        HasCancellationStatus = true;
    }

    /// <summary>Describes this picture (task 019): the text is shown, and it goes into the input field only on the user's action.</summary>
    [RelayCommand(CanExecute = nameof(CanDescribe))]
    private Task DescribeAsync() => _describe(this, DescriptionMode.Description);

    [RelayCommand(CanExecute = nameof(CanDescribe))]
    private Task DescribePromptAsync() => _describe(this, DescriptionMode.Prompt);

    private bool CanDescribe() => IsFinished && !IsVideo && !IsAudio;

    /// <summary>Shows the full error text of a failed generation (task 076). Only a failed card has it.</summary>
    [RelayCommand(CanExecute = nameof(IsFailed))]
    private Task DetailsAsync() => _dialogs.AlertAsync(AppResources.ErrorDetailsTitle, ErrorDetailsValue());

    /// <summary>What the details dialog shows: the provider's text when there is one, otherwise the text for the code.</summary>
    private string ErrorDetailsValue() => TechnicalError.Length > 0 ? TechnicalError : ErrorText;

    /// <summary>Opens the model picker to run this prompt and seed on another model (failed or filtered card, task 076).</summary>
    [RelayCommand(CanExecute = nameof(CanRunOnOtherModel))]
    private Task OtherModelAsync() => _otherModel(this);

    private bool CanRunOnOtherModel() => IsFailedOrFiltered && !IsVideo && !IsAudio;

    /// <summary>Copies the exact prompt to the clipboard (any finished card, task 076).</summary>
    [RelayCommand(CanExecute = nameof(IsFinished))]
    private Task CopyPromptAsync() => _clipboard.SetTextAsync(Prompt);

    /// <summary>
    /// Opens the actions menu (phone action sheet or right-click/"More" context menu). The title is the card meta line
    /// (model, seed, cost, time), the options are the same actions as the icon row, and the chosen one runs.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsFinished))]
    private async Task ShowActionsAsync()
    {
        var actions = new List<(string Label, Func<GenerationItemViewModel, Task> Run)>
        {
            (AppResources.Repeat, _repeat),
            (AppResources.ActionEditPrompt, _edit),
            (AppResources.CopyPrompt, card => card._clipboard.SetTextAsync(card.Prompt)),
        };
        if (!IsAudio)
        {
            actions.Insert(1, (AppResources.NewSeed, _newSeed));
        }
        if (!IsVideo && !IsAudio)
        {
            actions.Insert(3, (AppResources.ActionPromptFromImage, card => _describe(card, DescriptionMode.Prompt)));
            actions.Insert(3, (AppResources.ActionDescribeImage, card => _describe(card, DescriptionMode.Description)));
        }

        if (HasMedia)
        {
            actions.Add((SaveLabel, card => card.SaveAsync()));
        }

        if (HasImage && _compare is not null)
        {
            actions.Add((AppResources.Compare, card => card._compare!(card)));
        }

        if (IsFailedOrFiltered && !IsVideo && !IsAudio)
        {
            actions.Add((AppResources.OtherModel, card => card._otherModel(card)));
        }

        if (IsFailed)
        {
            actions.Add((AppResources.ErrorDetailsTitle, card => card._dialogs.AlertAsync(AppResources.ErrorDetailsTitle, card.ErrorDetailsValue())));
        }

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

        IsVideo = generation.Kind == MediaKind.Video;
        IsImage = generation.Kind == MediaKind.Image;
        IsAudio = generation.Kind == MediaKind.Audio;
        var imagePath = generation.Status == GenerationStatus.Succeeded ? generation.ImagePath ?? "" : "";
        if (!string.Equals(imagePath, _generationImagePath, StringComparison.Ordinal))
        {
            ImageDimensionsText = "";
            HasImageDimensions = false;
            VideoPosterPath = "";
            ImageAreaHeight = IsVideo ? VideoHeightForAspectRatio(_videoAspectRatio) : CardImageLayout.HeightFor(null, null);
            lock (_videoPosterLock) _videoPosterRead = null;
        }
        _generationImagePath = imagePath;

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
        DurationText = generation.DurationSeconds is { } seconds
            ? string.Format(CultureInfo.CurrentCulture, AppResources.VideoDurationFormat, seconds.ToString("0.##", CultureInfo.CurrentCulture))
            : string.Empty;
        IsQueued = generation.Status == GenerationStatus.Queued;
        IsActive = generation.Status == GenerationStatus.Running;
        IsFailed = generation.Status == GenerationStatus.Failed;
        IsFiltered = generation.Status == GenerationStatus.Filtered;
        IsFailedOrFiltered = (IsFailed || IsFiltered) && generation.Kind == MediaKind.Image;
        IsCancelled = generation.Status == GenerationStatus.Cancelled;
        // The text for the code is shown; the provider's own text (generation.ErrorMessage) is kept for the details dialog.
        TechnicalError = generation.ErrorMessage?.Trim() ?? "";
        ErrorText = generation.ErrorKind is { } kind ? ErrorKindText.Of(kind) : TechnicalError;
        HasErrorText = ErrorText.Length > 0;
        HasError = generation.Status == GenerationStatus.Failed && HasErrorText;
        HasMedia = generation.Status == GenerationStatus.Succeeded && !string.IsNullOrEmpty(generation.ImagePath);
        var fullPath = HasMedia ? _files.GetFullPath(generation.ImagePath!) : "";
        MediaFilePath = fullPath;
        VideoFilePath = IsVideo && HasMedia ? fullPath : "";
        AudioFilePath = IsAudio && HasMedia ? fullPath : "";
        if (IsVideo && VideoPosterPath.Length == 0)
        {
            ImageAreaHeight = VideoHeightForAspectRatio(_videoAspectRatio);
        }
        OnPropertyChanged(nameof(CanPlayMedia));
        PlayMediaCommand.NotifyCanExecuteChanged();
        HasImage = HasMedia && generation.Kind == MediaKind.Image;
        ImageFilePath = HasImage ? fullPath : "";
        _mediaType = generation.MediaType ?? "image/png";
        OnPropertyChanged(nameof(SaveLabel));
        IsRunning = generation.Status is GenerationStatus.Queued or GenerationStatus.Running;
        IsCancelRequested = IsRunning && generation.CancelRequestedAt is not null;
        if (_stoppedWaiting && (IsVideo || IsAudio) && IsCancelled)
        {
            CancellationStatusText = AppResources.StoppedWaitingMedia;
            HasCancellationStatus = true;
        }
        else if (generation.IsFinished || (!IsCancelRequested && CancellationStatusText != AppResources.CancelFailed))
        {
            CancellationStatusText = "";
            HasCancellationStatus = false;
        }
        else if (IsCancelRequested)
        {
            CancellationStatusText = AppResources.CancelRequested;
            HasCancellationStatus = true;
        }
        IsFinished = generation.IsFinished;
        DescribeCommand.NotifyCanExecuteChanged();
        DescribePromptCommand.NotifyCanExecuteChanged();
        OtherModelCommand.NotifyCanExecuteChanged();
        UpdateSummaryText();
    }

    public void ApplyImageDimensions(ImageDimensions? dimensions)
    {
        ImageAreaHeight = CardImageLayout.HeightFor(dimensions?.Width, dimensions?.Height);
        if (dimensions is { } value)
        {
            var divisor = GreatestCommonDivisor(value.Width, value.Height);
            ImageDimensionsText = string.Format(
                CultureInfo.InvariantCulture,
                AppResources.ImageDimensionsFormat,
                value.Width,
                value.Height,
                value.Width / divisor,
                value.Height / divisor);
            HasImageDimensions = true;
        }
        else
        {
            ImageDimensionsText = "";
            HasImageDimensions = false;
        }

        UpdateSummaryText();
    }

    private void UpdateSummaryText() => SummaryText = string.Join(
        ". ",
        new[] { StatusText, DetailsText, DurationText, ImageDimensionsText, ErrorText }.Where(text => text.Length > 0));

    private static int GreatestCommonDivisor(int left, int right)
    {
        while (right != 0)
        {
            (left, right) = (right, left % right);
        }

        return left;
    }

    private static double VideoHeightForAspectRatio(string? aspectRatio)
    {
        var parts = aspectRatio?.Split(':', StringSplitOptions.TrimEntries);
        if (parts is { Length: 2 }
            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)
            && width > 0
            && height > 0)
        {
            return CardImageLayout.HeightFor(width, height);
        }

        return CardImageLayout.HeightFor(16, 9);
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
            return generation.Kind == MediaKind.Audio && generation.Status == GenerationStatus.Succeeded
                ? AppResources.AudioPriceUnknown
                : "";
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

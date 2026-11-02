using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Descriptions;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Spending;
using AppTaskExtensions = ImageGen.Presentation.Utilities.TaskExtensions;

namespace ImageGen.Presentation.PageModels;

/// <summary>
/// Chat of one session: messages and runs in timeline order, the input line, and the actions on generation cards.
/// The page reloads the whole timeline after its own actions; <see cref="GenerationService.GenerationUpdated"/>
/// updates only the card that changed.
/// </summary>
public sealed partial class ChatPageModel(
    SessionService sessions,
    AssistantService assistant,
    GenerationService generations,
    IRunRepository runs,
    IImageFileStore files,
    INavigationService navigation,
    IDialogService dialogs,
    IMainThread mainThread,
    AttachmentService attachments,
    IFilePickerService filePicker,
    ImageDescriptionService descriptions,
    IClipboardService clipboard,
    IImageSaver imageSaver,
    ICurrentSessionTracker currentSession,
    Func<SessionModelsPageModel> modelsPaneFactory,
    Func<SessionParametersPageModel> parametersPaneFactory,
    IImageViewerService imageViewer,
    ErrorAlert errorAlert,
    CostEstimator? costEstimator = null,
    SpendingService? spendingService = null,
    IImageComparisonService? imageComparison = null) : ObservableObject
{
    private readonly ErrorAlert _errorAlert = errorAlert;
    private readonly CostEstimator? _costEstimator = costEstimator;
    private readonly SpendingService? _spendingService = spendingService;
    private readonly IImageComparisonService? _imageComparison = imageComparison;
    private readonly Dictionary<Guid, GenerationItemViewModel> _cards = [];
    private bool _imageConsentGiven;
    private Guid _sessionId;
    private string? _pendingMessage;
    private Guid? _sentMessageSessionId;
    private bool _subscribed;
    private CancellationTokenSource _imageDimensionsCancellation = new();
    private readonly Dictionary<(Guid Id, string Path), Task<ImageDimensions?>> _imageDimensionsReads = [];
    private int _imageDimensionsEpoch;

    /// <summary>Rows of the chat: <see cref="MessageItemViewModel"/> or <see cref="RunItemViewModel"/>.</summary>
    public ObservableCollection<object> Items { get; } = [];

    /// <summary>Raised after an in-place generation card update, so views can follow row content changes.</summary>
    public event EventHandler? CardUpdated;

    [ObservableProperty]
    public partial string Title { get; set; } = "";

    [ObservableProperty]
    public partial string ModelsButtonText { get; set; } = "";

    /// <summary>The assistant model of the session, shown next to the models button.</summary>
    [ObservableProperty]
    public partial string AssistantText { get; set; } = "";

    [ObservableProperty]
    public partial string NextRunEstimateText { get; set; } = "";

    [ObservableProperty]
    public partial bool HasNextRunEstimate { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string InputText { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AttachImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsBusy { get; set; }

    partial void OnIsBusyChanged(bool value) => UpdateRetry();

    /// <summary>
    /// The card the user touched last. The menu bar's generation actions act on it (task 073); a card that is no longer
    /// shown stays selected until another one is touched.
    /// </summary>
    [ObservableProperty]
    public partial GenerationItemViewModel? SelectedCard { get; set; }

    [RelayCommand]
    private void SelectCard(GenerationItemViewModel card) => SelectedCard = card;

    [RelayCommand]
    private async Task OpenImageAsync(GenerationItemViewModel? card)
    {
        if (card is not { HasImage: true })
        {
            return;
        }

        var run = Items.OfType<RunItemViewModel>().FirstOrDefault(item => item.Generations.Contains(card));
        if (run is null)
        {
            return;
        }

        var images = run.Generations.Where(item => item.HasImage).ToList();
        var initialIndex = images.IndexOf(card);
        if (initialIndex < 0)
        {
            return;
        }

        SelectedCard = card;
        await imageViewer.ShowAsync(
            images.Select(item => new ImageViewerItem(
                item.Id,
                item.ImageFilePath,
                string.Join(", ", new[] { item.ModelText, item.SeedText, item.ImageDimensionsText, item.SummaryText }
                    .Where(text => !string.IsNullOrWhiteSpace(text))),
                item.RelativeImagePath)).ToList(),
            initialIndex,
            CancellationToken.None);
    }

    /// <summary>True on a wide window (≥ 840). The shell sets it on resize; it decides pane versus navigation.</summary>
    [ObservableProperty]
    public partial bool IsWide { get; set; }

    /// <summary>The models page model shown in the side pane, or null when the pane is closed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActivePane))]
    [NotifyPropertyChangedFor(nameof(IsPaneOpen))]
    [NotifyPropertyChangedFor(nameof(PaneTitle))]
    public partial SessionModelsPageModel? ModelsPane { get; set; }

    /// <summary>The parameters page model shown in the side pane, or null when the pane is closed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActivePane))]
    [NotifyPropertyChangedFor(nameof(IsPaneOpen))]
    [NotifyPropertyChangedFor(nameof(PaneTitle))]
    public partial SessionParametersPageModel? ParametersPane { get; set; }

    /// <summary>The page model shown in the side pane now (models or parameters), or null when it is closed.</summary>
    public object? ActivePane => (object?)ModelsPane ?? ParametersPane;

    /// <summary>True while a side pane is open; the shell shows the third column.</summary>
    public bool IsPaneOpen => ActivePane is not null;

    /// <summary>The header of the open pane: the models or the parameters title.</summary>
    public string PaneTitle => ModelsPane is not null
        ? AppResources.ModelsTitle
        : ParametersPane is not null ? AppResources.SessionParametersTitle : "";

    /// <summary>Reads the session id from the route query. The page forwards the Shell query here.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!query.TryGetValue(Routes.SessionIdParameter, out var value)
            || value is not string text
            || !Guid.TryParse(text, out var sessionId))
        {
            throw new ArgumentException(
                $"The chat route needs a valid '{Routes.SessionIdParameter}' query parameter.", nameof(query));
        }

        if (_sessionId != sessionId)
        {
            CancelImageDimensionsReads();
            _sessionId = sessionId;
        }
        // The same route comes back when the page reappears after a modal (the image viewer): its message was sent already.
        var alreadySent = _sentMessageSessionId == sessionId;
        _pendingMessage = !alreadySent && query.TryGetValue(Routes.MessageParameter, out var message) && message is string encoded
            ? Uri.UnescapeDataString(encoded)
            : null;
    }

    /// <summary>Called on every appearance: subscribes once to generation updates, then loads the chat.</summary>
    public async Task OnAppearingAsync()
    {
        Unsubscribe();
        generations.GenerationUpdated += OnGenerationUpdated;
        _subscribed = true;
        await ReloadAsync();

        // A message that came with the route (an example on the empty sessions page) is sent once, after the first load.
        if (_pendingMessage is { Length: > 0 } pending)
        {
            _pendingMessage = null;
            _sentMessageSessionId = _sessionId;
            InputText = pending;
            await SendCommand.ExecuteAsync(null);
        }
    }

    public void OnDisappearing() => Unsubscribe();

    /// <summary>Reads the whole timeline of the session and rebuilds the rows and cards.</summary>
    public async Task ReloadAsync()
    {
        var epoch = BeginImageDimensionsEpoch();
        var sessionId = _sessionId;
        currentSession.CurrentId = _sessionId;
        try
        {
            var timeline = await sessions.GetTimelineAsync(_sessionId, CancellationToken.None);
            if (epoch != _imageDimensionsEpoch || sessionId != _sessionId)
            {
                return;
            }
            var settings = timeline.Session.Settings;
            Title = timeline.Session.Title;
            ModelsButtonText = string.Format(
                CultureInfo.CurrentCulture,
                AppResources.ModelsButtonFormat,
                settings.ImageModels.Count,
                settings.RunsPerModel);
            AssistantText = string.Format(CultureInfo.CurrentCulture, AppResources.AssistantLabelFormat, settings.AssistantModel.ModelId);

            Items.Clear();
            _cards.Clear();
            var entries = TimelineOrdering.Order(timeline);
            var promptByRunId = entries.Where(e => e.Run is not null).ToDictionary(e => e.Run!.Id, e => e.Run!.Prompt);
            foreach (var entry in entries)
            {
                if (entry.Message is { } message)
                {
                    if (message.RunId is { } runId && promptByRunId.TryGetValue(runId, out var runPrompt))
                    {
                        var text = RunMessageText.Visible(message.Text, runPrompt);
                        if (text.Length > 0)
                        {
                            Items.Add(new MessageItemViewModel(message, text, new CopyTextAction(clipboard, mainThread, text), _ => RetryLastMessageAsync()));
                        }
                    }
                    else
                    {
                        Items.Add(new MessageItemViewModel(message, message.Text, new CopyTextAction(clipboard, mainThread, message.Text), _ => RetryLastMessageAsync()));
                    }
                }
                else if (entry.Run is { } run)
                {
                    var cards = entry.Generations.Select(CreateCard).ToList();
                    Items.Add(new RunItemViewModel(run, cards, new CopyTextAction(clipboard, mainThread, run.Prompt), CancelRunAsync));
                    foreach (var generation in entry.Generations)
                    {
                        if (_cards.TryGetValue(generation.Id, out var card))
                        {
                            StartImageDimensionsRead(generation, card, sessionId, epoch);
                        }
                    }
                }
            }

            var summary = await sessions.GetSummaryAsync(_sessionId, CancellationToken.None);
            if (summary is not null)
            {
                InsertSummaryMarker(summary);
            }

            UpdateRetry();
            await UpdateNextRunEstimateAsync(settings.ImageModels, settings.RunsPerModel, settings.Parameters);
        }
        catch (Exception ex)
        {
            await _errorAlert.ShowAsync(ex);
        }
    }

    /// <summary>Images attached to the message being written (up to 4). They are sent with the message.</summary>
    public ObservableCollection<PendingAttachment> PendingAttachments { get; } = [];

    [ObservableProperty]
    public partial string AttachmentsText { get; set; } = "";

    [RelayCommand(CanExecute = nameof(CanAttach))]
    private async Task AttachImageAsync()
    {
        try
        {
            if (PendingAttachments.Count >= AttachmentService.MaxAttachmentsPerMessage)
            {
                await dialogs.AlertAsync(
                    AppResources.ErrorTitle,
                    string.Format(CultureInfo.CurrentCulture, AppResources.AttachmentLimitMessage, AttachmentService.MaxAttachmentsPerMessage));
                return;
            }

            // The provider sees the images, so the user confirms once before the first one goes out (F10.6).
            if (!_imageConsentGiven)
            {
                var accepted = await dialogs.ConfirmAsync(
                    AppResources.ImageConsentTitle, AppResources.ImageConsentMessage, AppResources.ImageConsentAccept, AppResources.Cancel);
                if (!accepted)
                {
                    return;
                }

                _imageConsentGiven = true;
            }

            var picked = await filePicker.PickImageAsync(CancellationToken.None);
            if (picked is null)
            {
                return;
            }

            var attachment = await attachments.AddImageAsync(_sessionId, picked.Content, picked.MediaType, CancellationToken.None);
            PendingAttachments.Add(new PendingAttachment(attachment.Id, picked.FileName, RemoveAttachmentCommand));
            RefreshAttachments();
        }
        catch (Exception ex)
        {
            await _errorAlert.ShowAsync(ex);
        }
    }

    [RelayCommand]
    private void RemoveAttachment(PendingAttachment? attachment)
    {
        if (attachment is not null && PendingAttachments.Remove(attachment))
        {
            RefreshAttachments();
        }
    }

    private bool CanAttach() => !IsBusy;

    private void RefreshAttachments()
    {
        AttachmentsText = PendingAttachments.Count == 0
            ? ""
            : string.Format(
                CultureInfo.CurrentCulture,
                AppResources.AttachmentsCountFormat,
                PendingAttachments.Count,
                AttachmentService.MaxAttachmentsPerMessage);
        SendCommand.NotifyCanExecuteChanged();
        AttachImageCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = InputText;
        var pending = PendingAttachments.ToList();
        if (_costEstimator is not null && _spendingService is not null)
        {
            var estimate = await EstimateAsync(_sessionId, CancellationToken.None);
            if (estimate is not null
                && await _spendingService.CheckAsync(estimate, CancellationToken.None) == SpendingWarning.MonthlyLimitReached)
            {
                await dialogs.AlertAsync(AppResources.SpendingWarningTitle, AppResources.SpendingMonthlyLimitMessage);
            }
        }

        InputText = "";
        PendingAttachments.Clear();
        RefreshAttachments();
        IsBusy = true;
        try
        {
            await assistant.SendUserMessageAsync(_sessionId, text, pending.Select(a => a.Id).ToList(), CancellationToken.None);
        }
        catch (Exception ex)
        {
            if (InputText.Length == 0)
            {
                InputText = text;
            }

            if (PendingAttachments.Count == 0)
            {
                foreach (var attachment in pending)
                {
                    PendingAttachments.Add(attachment);
                }

                RefreshAttachments();
            }

            await _errorAlert.ShowAsync(ex);
        }
        finally
        {
            IsBusy = false;
        }

        await ReloadAsync();
    }

    private bool CanSend() => !IsBusy && (!string.IsNullOrWhiteSpace(InputText) || PendingAttachments.Count > 0);

    /// <summary>C4.1: the marker goes after the last message the summary covers; without that message it goes first.</summary>
    private void InsertSummaryMarker(ConversationSummary summary)
    {
        var covered = -1;
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i] is MessageItemViewModel message && message.Id == summary.CoversUpToMessageId)
            {
                covered = i;
                break;
            }
        }

        Items.Insert(covered + 1, new SummaryMarkerViewModel(summary, dialogs));
    }

    /// <summary>S2.5: «Try again» is offered only for the last message of the chat, and only while nothing runs.</summary>
    private void UpdateRetry()
    {
        foreach (var item in Items.OfType<MessageItemViewModel>())
        {
            item.CanRetry = false;
        }

        if (!IsBusy && Items.LastOrDefault() is MessageItemViewModel { IsError: true } last)
        {
            last.CanRetry = true;
        }
    }

    /// <summary>Sends the chat's last message to the assistant again (S2.5, task 077); the error reply stays in the chat.</summary>
    private async Task RetryLastMessageAsync()
    {
        IsBusy = true;
        try
        {
            await assistant.RetryLastTurnAsync(_sessionId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            await _errorAlert.ShowAsync(ex);
        }
        finally
        {
            IsBusy = false;
        }

        await ReloadAsync();
    }

    [RelayCommand]
    private async Task OpenParametersAsync()
    {
        try
        {
            if (!IsWide)
            {
                await navigation.GoToAsync(Routes.SessionParametersFor(_sessionId));
                return;
            }

            // The same button toggles the pane; a different button replaces whatever is open.
            if (ParametersPane is not null)
            {
                ClosePane();
                return;
            }

            ClosePane();
            var pane = parametersPaneFactory();
            pane.HostedInPane = true;
            pane.CloseRequested += OnPaneCloseRequested;
            pane.ApplyQueryAttributes(new Dictionary<string, object> { [Routes.SessionIdParameter] = _sessionId.ToString("D") });
            ParametersPane = pane;
            await pane.OnAppearingAsync();
        }
        catch (Exception ex)
        {
            await _errorAlert.ShowAsync(ex);
        }
    }

    [RelayCommand]
    private async Task OpenModelsAsync()
    {
        try
        {
            if (!IsWide)
            {
                await navigation.GoToAsync(Routes.SessionModelsFor(_sessionId));
                return;
            }

            // The same button toggles the pane; a different button replaces whatever is open.
            if (ModelsPane is not null)
            {
                ClosePane();
                return;
            }

            ClosePane();
            var pane = modelsPaneFactory();
            pane.HostedInPane = true;
            pane.CloseRequested += OnPaneCloseRequested;
            pane.ApplyQueryAttributes(new Dictionary<string, object> { [Routes.SessionIdParameter] = _sessionId.ToString("D") });
            ModelsPane = pane;
            await pane.OnAppearingAsync();
        }
        catch (Exception ex)
        {
            await _errorAlert.ShowAsync(ex);
        }
    }

    /// <summary>Closes the open pane (the close button and the pane's own Save/Cancel).</summary>
    [RelayCommand]
    private void ClosePane()
    {
        if (ModelsPane is { } models)
        {
            models.CloseRequested -= OnPaneCloseRequested;
            ModelsPane = null;
        }

        if (ParametersPane is { } parameters)
        {
            parameters.CloseRequested -= OnPaneCloseRequested;
            ParametersPane = null;
        }
    }

    private void OnPaneCloseRequested(object? sender, EventArgs e) => ClosePane();

    private Task RepeatCardAsync(GenerationItemViewModel card) =>
        RunAndReloadAsync(async () =>
        {
            if (await ConfirmEstimatedRunAsync(card, null))
            {
                await generations.RepeatAsync(card.Id, CancellationToken.None);
            }
        });

    private Task NewSeedCardAsync(GenerationItemViewModel card) =>
        RunAndReloadAsync(async () =>
        {
            if (await ConfirmEstimatedRunAsync(card, null))
            {
                await generations.NewSeedAsync(card.Id, CancellationToken.None);
            }
        });

    private Task EditCardAsync(GenerationItemViewModel card) =>
        RunAndReloadAsync(async () =>
        {
            var text = await dialogs.PromptAsync(
                AppResources.EditPromptTitle,
                AppResources.EditPromptMessage,
                AppResources.Generate,
                AppResources.Cancel,
                card.Prompt);
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (await ConfirmEstimatedRunAsync(card, null))
                {
                    await generations.EditAsync(card.Id, text.Trim(), CancellationToken.None);
                }
            }
        });

    private async Task<bool> ConfirmEstimatedRunAsync(GenerationItemViewModel card, ModelRef? targetModel)
    {
        if (_costEstimator is null || _spendingService is null)
        {
            return true;
        }

        var run = Items.OfType<RunItemViewModel>().FirstOrDefault(item => item.Generations.Contains(card));
        if (run is null)
        {
            return true;
        }

        var models = targetModel is { } target
            ? new[] { target }
            : [new ModelRef(card.ProviderId, card.ModelId)];
        var estimate = await _costEstimator.EstimateAsync(models, 1, run.Parameters, CancellationToken.None);
        if (await _spendingService.CheckAsync(estimate, CancellationToken.None) == SpendingWarning.None)
        {
            return true;
        }

        var message = string.Format(
            CultureInfo.CurrentCulture,
            AppResources.SpendingConfirmationFormat,
            FormatEstimate(estimate));
        return await dialogs.ConfirmAsync(AppResources.SpendingWarningTitle, message, AppResources.Generate, AppResources.Cancel);
    }

    private async Task<CostEstimate?> EstimateAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (_costEstimator is null)
        {
            return null;
        }

        var session = await sessions.GetAsync(sessionId, cancellationToken);
        return session is null
            ? null
            : await _costEstimator.EstimateAsync(session.Settings.ImageModels, session.Settings.RunsPerModel, session.Settings.Parameters, cancellationToken);
    }

    private async Task UpdateNextRunEstimateAsync(
        IReadOnlyList<ModelRef> models,
        int runsPerModel,
        ImageGen.Core.Domain.Sessions.GenerationParameters parameters)
    {
        if (_costEstimator is null)
        {
            HasNextRunEstimate = false;
            NextRunEstimateText = "";
            return;
        }

        try
        {
            var estimate = await _costEstimator.EstimateAsync(models, runsPerModel, parameters, CancellationToken.None);
            NextRunEstimateText = string.Format(
                CultureInfo.CurrentCulture,
                AppResources.NextRunEstimateFormat,
                FormatEstimate(estimate));
            HasNextRunEstimate = true;
        }
        catch (Exception)
        {
            NextRunEstimateText = AppResources.AssistantPriceUnknown;
            HasNextRunEstimate = true;
        }
    }

    private static string FormatEstimate(CostEstimate estimate)
    {
        var price = estimate.KnownUsd.ToString("0.####", CultureInfo.InvariantCulture);
        var amount = string.Format(CultureInfo.InvariantCulture, "${0}", price);
        if (estimate.IsApproximate)
        {
            amount = "≈" + amount;
        }

        return estimate.UnknownModels.Count == 0
            ? amount
            : string.Format(CultureInfo.CurrentCulture, AppResources.CostEstimateWithUnknownFormat, amount, estimate.UnknownModels.Count);
    }

    private async Task RunAndReloadAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await _errorAlert.ShowAsync(ex);
        }

        await ReloadAsync();
    }

    /// <summary>Opens the model picker for a card: pick a model, run the same prompt and seed on it (task 076).</summary>
    private Task OtherModelCardAsync(GenerationItemViewModel card) =>
        navigation.GoToAsync(Routes.ModelPickerFor(_sessionId, card.Id));

    private async Task CancelCardAsync(GenerationItemViewModel card)
    {
        var result = await generations.CancelGenerationAsync(card.Id, CancellationToken.None);
        if (result == GenerationCancelResult.CancelFailed)
        {
            card.SetCancellationFailed();
        }
    }

    private async Task CancelRunAsync(Guid runId)
    {
        var results = await generations.CancelRunAsync(runId, CancellationToken.None);
        foreach (var result in results.Where(item => item.Result == GenerationCancelResult.CancelFailed))
        {
            if (_cards.TryGetValue(result.GenerationId, out var card)) card.SetCancellationFailed();
        }
    }

    private async Task CompareCardAsync(GenerationItemViewModel card)
    {
        if (_imageComparison is null)
        {
            return;
        }

        var rows = await runs.ListGenerationsBySessionAsync(_sessionId, CancellationToken.None);
        var candidates = rows.Where(row => row.Id != card.Id
                && row.SessionId == _sessionId
                && row.Status == GenerationStatus.Succeeded
                && !string.IsNullOrWhiteSpace(row.ImagePath))
            .ToList();
        if (candidates.Count == 0)
        {
            await dialogs.AlertAsync(AppResources.ComparisonTitle, AppResources.ComparisonNoCandidates);
            return;
        }

        var options = candidates.Select(row => string.Format(
            CultureInfo.CurrentCulture,
            AppResources.ComparisonCandidateFormat,
            row.ModelId,
            row.SeedApplied ? row.Seed.ToString(CultureInfo.CurrentCulture) : AppResources.SeedNotSupported,
            row.CostUsd is { } cost ? "$" + cost.ToString("0.####", CultureInfo.CurrentCulture) : AppResources.DescriptionCostUnknown,
            row.Id.ToString("N")[..8])).ToList();
        var selected = await dialogs.ChooseAsync(AppResources.ComparisonTitle, AppResources.Cancel, options);
        var selectedIndex = selected is null ? -1 : options.IndexOf(selected);
        if (selectedIndex < 0)
        {
            return;
        }

        var source = await runs.GetGenerationAsync(card.Id, CancellationToken.None);
        var other = await runs.GetGenerationAsync(candidates[selectedIndex].Id, CancellationToken.None);
        if (source is null || other is null
            || source.SessionId != _sessionId || other.SessionId != _sessionId
            || source.Status != GenerationStatus.Succeeded || other.Status != GenerationStatus.Succeeded
            || string.IsNullOrWhiteSpace(source.ImagePath) || string.IsNullOrWhiteSpace(other.ImagePath)
            || await sessions.GetAsync(_sessionId, CancellationToken.None) is null
            || !File.Exists(files.GetFullPath(source.ImagePath))
            || !File.Exists(files.GetFullPath(other.ImagePath)))
        {
            await dialogs.AlertAsync(AppResources.ErrorTitle, AppResources.ComparisonUnavailable);
            return;
        }

        await _imageComparison.ShowAsync(new ComparisonPageModel(ToComparisonImage(source), ToComparisonImage(other)), CancellationToken.None);
    }

    private ComparisonImage ToComparisonImage(Generation generation) => new(
        generation.Id,
        files.GetFullPath(generation.ImagePath!),
        generation.ModelId,
        generation.SeedApplied ? generation.Seed.ToString(CultureInfo.CurrentCulture) : AppResources.SeedNotSupported,
        generation.CostUsd is { } cost ? "$" + cost.ToString("0.####", CultureInfo.CurrentCulture) : AppResources.DescriptionCostUnknown);

    private GenerationItemViewModel CreateCard(Generation generation)
    {
        var card = new GenerationItemViewModel(
            generation, files, dialogs, clipboard, imageSaver, RepeatCardAsync, NewSeedCardAsync, EditCardAsync, DescribeCardAsync, OtherModelCardAsync, CompareCardAsync, CancelCardAsync);
        _cards[generation.Id] = card;
        return card;
    }

    /// <summary>Describes a card's picture with the assistant model, then shows the text (task 019).</summary>
    public async Task DescribeCardAsync(GenerationItemViewModel card, DescriptionMode mode)
    {
        try
        {
            var language = CultureInfo.CurrentUICulture.EnglishName;
            var description = await descriptions.DescribeAsync(_sessionId, card.Id, mode, language, CancellationToken.None);
            await ShowDescriptionAsync(description);
        }
        catch (Exception ex)
        {
            await _errorAlert.ShowAsync(ex);
        }
    }

    /// <summary>
    /// Shows the description with its model, provider and price. The text goes into the input field only when the user
    /// chooses it; if the field has text, the user chooses between replacing it and appending.
    /// </summary>
    public async Task ShowDescriptionAsync(ImageDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        var price = description.CostUsd is { } cost
            ? "$" + cost.ToString("0.####", CultureInfo.InvariantCulture)
            : AppResources.DescriptionCostUnknown;
        var source = string.Format(CultureInfo.CurrentCulture, AppResources.DescriptionFromFormat, description.ProviderId, description.ModelId, price);
        var insert = await dialogs.ConfirmAsync(
            AppResources.DescriptionTitle,
            description.Text + "\n\n" + source,
            AppResources.DescriptionInsert,
            AppResources.DescriptionClose);
        if (!insert)
        {
            return;
        }

        if (InputText.Trim().Length == 0)
        {
            InputText = description.Text;
            return;
        }

        var replace = await dialogs.ConfirmAsync(
            AppResources.DescriptionTitle,
            AppResources.DescriptionReplaceQuestion,
            AppResources.DescriptionReplace,
            AppResources.DescriptionAppend);
        InputText = replace ? description.Text : InputText + "\n" + description.Text;
    }

    /// <summary>Raised on a background thread. Only the card of this session is updated, on the UI thread.</summary>
    private void OnGenerationUpdated(object? sender, GenerationUpdatedEventArgs e)
    {
        var generation = e.Generation;
        if (generation.SessionId != _sessionId)
        {
            return;
        }

        var sessionId = _sessionId;
        var epoch = _imageDimensionsEpoch;
        mainThread.BeginInvokeOnMainThread(() =>
        {
            if (sessionId == _sessionId && epoch == _imageDimensionsEpoch && _cards.TryGetValue(generation.Id, out var card))
            {
                card.Update(generation);
                StartImageDimensionsRead(generation, card, sessionId, epoch);
                CardUpdated?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    private void Unsubscribe()
    {
        CancelImageDimensionsReads();
        if (_subscribed)
        {
            generations.GenerationUpdated -= OnGenerationUpdated;
            _subscribed = false;
        }
    }

    private int BeginImageDimensionsEpoch()
    {
        CancelImageDimensionsReads();
        _imageDimensionsCancellation.Dispose();
        _imageDimensionsCancellation = new CancellationTokenSource();
        _imageDimensionsReads.Clear();
        return ++_imageDimensionsEpoch;
    }

    private void CancelImageDimensionsReads()
    {
        _imageDimensionsEpoch++;
        _imageDimensionsCancellation.Cancel();
    }

    private void StartImageDimensionsRead(Generation generation, GenerationItemViewModel card, Guid sessionId, int epoch)
    {
        if (generation.Status != GenerationStatus.Succeeded
            || string.IsNullOrWhiteSpace(generation.ImagePath)
            || !card.HasImage)
        {
            return;
        }

        var relativePath = generation.ImagePath;
        var key = (generation.Id, relativePath);
        if (!_imageDimensionsReads.TryGetValue(key, out var read))
        {
            var cancellationToken = _imageDimensionsCancellation.Token;
            read = Task.Run(async () => await files.TryReadDimensionsAsync(relativePath, cancellationToken), cancellationToken);
            _imageDimensionsReads[key] = read;
        }

        AppTaskExtensions.FireAndForgetSafeAsync(
            ApplyImageDimensionsWhenReadyAsync(read, generation.Id, relativePath, card, sessionId, epoch, _imageDimensionsCancellation.Token));
    }

    private async Task ApplyImageDimensionsWhenReadyAsync(
        Task<ImageDimensions?> read,
        Guid generationId,
        string relativePath,
        GenerationItemViewModel card,
        Guid sessionId,
        int epoch,
        CancellationToken cancellationToken)
    {
        ImageDimensions? dimensions;
        try
        {
            dimensions = await read;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        mainThread.BeginInvokeOnMainThread(() =>
        {
            if (cancellationToken.IsCancellationRequested
                || epoch != _imageDimensionsEpoch
                || sessionId != _sessionId
                || !_cards.TryGetValue(generationId, out var currentCard)
                || !ReferenceEquals(currentCard, card)
                || !card.IsDone
                || !card.HasImage
                || !string.Equals(files.GetFullPath(relativePath), card.ImageFilePath, StringComparison.Ordinal))
            {
                return;
            }

            card.ApplyImageDimensions(dimensions);
            CardUpdated?.Invoke(this, EventArgs.Empty);
        });
    }
}

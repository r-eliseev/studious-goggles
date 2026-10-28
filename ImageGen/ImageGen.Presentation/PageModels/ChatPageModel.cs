using System.Collections.ObjectModel;
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
/// Chat of one session: messages and runs in timeline order, the input line, and the actions on generation cards.
/// The page reloads the whole timeline after its own actions; <see cref="GenerationService.GenerationUpdated"/>
/// updates only the card that changed.
/// </summary>
public sealed partial class ChatPageModel(
    SessionService sessions,
    AssistantService assistant,
    GenerationService generations,
    IImageFileStore files,
    INavigationService navigation,
    IDialogService dialogs,
    IMainThread mainThread,
    AttachmentService attachments,
    IFilePickerService filePicker,
    ImageDescriptionService descriptions,
    IClipboardService clipboard,
    ICurrentSessionTracker currentSession,
    Func<SessionModelsPageModel> modelsPaneFactory,
    Func<SessionParametersPageModel> parametersPaneFactory) : ObservableObject
{
    private readonly Dictionary<Guid, GenerationItemViewModel> _cards = [];
    private bool _imageConsentGiven;
    private Guid _sessionId;
    private bool _subscribed;

    /// <summary>Rows of the chat: <see cref="MessageItemViewModel"/> or <see cref="RunItemViewModel"/>.</summary>
    public ObservableCollection<object> Items { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; } = "";

    [ObservableProperty]
    public partial string ModelsButtonText { get; set; } = "";

    /// <summary>The assistant model of the session, shown next to the models button.</summary>
    [ObservableProperty]
    public partial string AssistantText { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string InputText { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AttachImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsBusy { get; set; }

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

        _sessionId = sessionId;
    }

    /// <summary>Called on every appearance: subscribes once to generation updates, then loads the chat.</summary>
    public async Task OnAppearingAsync()
    {
        Unsubscribe();
        generations.GenerationUpdated += OnGenerationUpdated;
        _subscribed = true;
        await ReloadAsync();
    }

    public void OnDisappearing() => Unsubscribe();

    /// <summary>Reads the whole timeline of the session and rebuilds the rows and cards.</summary>
    public async Task ReloadAsync()
    {
        currentSession.CurrentId = _sessionId;
        try
        {
            var timeline = await sessions.GetTimelineAsync(_sessionId, CancellationToken.None);
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
                            Items.Add(new MessageItemViewModel(message, text, new CopyTextAction(clipboard, mainThread, text)));
                        }
                    }
                    else
                    {
                        Items.Add(new MessageItemViewModel(message, message.Text, new CopyTextAction(clipboard, mainThread, message.Text)));
                    }
                }
                else if (entry.Run is { } run)
                {
                    var cards = entry.Generations.Select(CreateCard).ToList();
                    Items.Add(new RunItemViewModel(run, cards, new CopyTextAction(clipboard, mainThread, run.Prompt)));
                }
            }
        }
        catch (Exception ex)
        {
            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
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
            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
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

            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }

        await ReloadAsync();
    }

    private bool CanSend() => !IsBusy && (!string.IsNullOrWhiteSpace(InputText) || PendingAttachments.Count > 0);

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
            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
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
            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
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
        RunAndReloadAsync(() => generations.RepeatAsync(card.Id, CancellationToken.None));

    private Task NewSeedCardAsync(GenerationItemViewModel card) =>
        RunAndReloadAsync(() => generations.NewSeedAsync(card.Id, CancellationToken.None));

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
                await generations.EditAsync(card.Id, text.Trim(), CancellationToken.None);
            }
        });

    private async Task RunAndReloadAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
        }

        await ReloadAsync();
    }

    private GenerationItemViewModel CreateCard(Generation generation)
    {
        var card = new GenerationItemViewModel(generation, files, dialogs, RepeatCardAsync, NewSeedCardAsync, EditCardAsync, DescribeCardAsync);
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
            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
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

        mainThread.BeginInvokeOnMainThread(() =>
        {
            if (_cards.TryGetValue(generation.Id, out var card))
            {
                card.Update(generation);
            }
        });
    }

    private void Unsubscribe()
    {
        if (_subscribed)
        {
            generations.GenerationUpdated -= OnGenerationUpdated;
            _subscribed = false;
        }
    }
}

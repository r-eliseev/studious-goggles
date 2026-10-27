using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Maui.Resources.Strings;
using ImageGen.Maui.Services;

namespace ImageGen.Maui.PageModels;

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
    IMainThread mainThread) : ObservableObject
{
    private readonly Dictionary<Guid, GenerationItemViewModel> _cards = [];
    private Guid _sessionId;
    private bool _subscribed;

    /// <summary>Rows of the chat: <see cref="MessageItemViewModel"/> or <see cref="RunItemViewModel"/>.</summary>
    public ObservableCollection<object> Items { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; } = "";

    [ObservableProperty]
    public partial string ModelsButtonText { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string InputText { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsBusy { get; set; }

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
        try
        {
            var timeline = await sessions.GetTimelineAsync(_sessionId, CancellationToken.None);
            var settings = timeline.Session.Settings;
            Title = timeline.Session.Title;
            ModelsButtonText = string.Format(
                CultureInfo.CurrentCulture,
                AppResources.ModelsButtonFormat,
                settings.ImageModelIds.Count,
                settings.RunsPerModel);

            Items.Clear();
            _cards.Clear();
            foreach (var entry in TimelineOrdering.Order(timeline))
            {
                if (entry.Message is { } message)
                {
                    Items.Add(new MessageItemViewModel(message));
                }
                else if (entry.Run is { } run)
                {
                    var cards = entry.Generations.Select(CreateCard).ToList();
                    Items.Add(new RunItemViewModel(run, cards));
                }
            }
        }
        catch (Exception ex)
        {
            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = InputText;
        InputText = "";
        IsBusy = true;
        try
        {
            await assistant.SendUserMessageAsync(_sessionId, text, CancellationToken.None);
        }
        catch (Exception ex)
        {
            if (InputText.Length == 0)
            {
                InputText = text;
            }

            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }

        await ReloadAsync();
    }

    private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(InputText);

    [RelayCommand]
    private async Task OpenModelsAsync()
    {
        try
        {
            await navigation.GoToAsync(Routes.SessionModelsFor(_sessionId));
        }
        catch (Exception ex)
        {
            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
        }
    }

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
        var card = new GenerationItemViewModel(generation, files, RepeatCardAsync, NewSeedCardAsync, EditCardAsync);
        _cards[generation.Id] = card;
        return card;
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

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Maui.Resources.Strings;
using ImageGen.Maui.Services;

namespace ImageGen.Maui.PageModels;

public sealed partial class ChatPageModel(
    SessionService sessionService,
    AssistantService assistantService,
    GenerationService generationService,
    IImageFileStore files,
    INavigationService navigation,
    IDialogService dialogs) : ObservableObject, IQueryAttributable
{
    private readonly Dictionary<Guid, GenerationItemViewModel> _generations = [];
    private Guid _sessionId;

    /// <summary>Rows of the chat: <see cref="MessageItemViewModel"/> or <see cref="RunItemViewModel"/>.</summary>
    public ObservableCollection<object> Items { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ModelsButtonText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsBusy { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(Routes.SessionIdParameter, out var value)
            && Guid.TryParse(value?.ToString(), out var sessionId))
        {
            _sessionId = sessionId;
        }
    }

    /// <summary>Called by the page on every appearance: subscribes to updates and reloads the chat.</summary>
    public async Task OnAppearingAsync()
    {
        generationService.GenerationUpdated -= OnGenerationUpdated;
        generationService.GenerationUpdated += OnGenerationUpdated;
        await ReloadAsync();
    }

    /// <summary>Called by the page when it disappears.</summary>
    public void OnDisappearing() => generationService.GenerationUpdated -= OnGenerationUpdated;

    private async Task ReloadAsync()
    {
        try
        {
            var timeline = await sessionService.GetTimelineAsync(_sessionId, CancellationToken.None);
            Title = timeline.Session.Title;
            ModelsButtonText = string.Format(
                CultureInfo.CurrentCulture,
                AppResources.ModelsButtonFormat,
                timeline.Session.Settings.ImageModelIds.Count,
                timeline.Session.Settings.RunsPerModel);

            _generations.Clear();
            Items.Clear();
            foreach (var entry in TimelineOrdering.Order(timeline))
            {
                if (entry.Message is not null)
                {
                    Items.Add(new MessageItemViewModel(entry.Message));
                }
                else if (entry.Run is not null)
                {
                    var cards = entry.Generations
                        .Select(g => new GenerationItemViewModel(g, files, RepeatAsync, NewSeedAsync, EditAsync))
                        .ToList();
                    foreach (var card in cards)
                    {
                        _generations[card.Id] = card;
                    }

                    Items.Add(new RunItemViewModel(entry.Run, cards));
                }
            }
        }
        catch (Exception ex)
        {
            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
        }
    }

    private void OnGenerationUpdated(object? sender, GenerationUpdatedEventArgs e)
    {
        if (e.Generation.SessionId != _sessionId)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_generations.TryGetValue(e.Generation.Id, out var card))
            {
                card.Update(e.Generation);
            }
        });
    }

    private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(InputText);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = InputText;
        IsBusy = true;
        try
        {
            InputText = string.Empty;
            await assistantService.SendUserMessageAsync(_sessionId, text, CancellationToken.None);
        }
        catch (Exception ex)
        {
            InputText = text;
            await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }

        await ReloadAsync();
    }

    [RelayCommand]
    private Task OpenModelsAsync() => navigation.GoToAsync(Routes.SessionModelsFor(_sessionId));

    private Task RepeatAsync(GenerationItemViewModel card) =>
        RunActionAsync(() => generationService.RepeatAsync(card.Id, CancellationToken.None));

    private Task NewSeedAsync(GenerationItemViewModel card) =>
        RunActionAsync(() => generationService.NewSeedAsync(card.Id, CancellationToken.None));

    private async Task EditAsync(GenerationItemViewModel card)
    {
        var prompt = await dialogs.PromptAsync(
            AppResources.EditPromptTitle,
            AppResources.EditPromptMessage,
            AppResources.Generate,
            AppResources.Cancel,
            card.Prompt);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        await RunActionAsync(() => generationService.EditAsync(card.Id, prompt, CancellationToken.None));
    }

    private async Task RunActionAsync(Func<Task> action)
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
}

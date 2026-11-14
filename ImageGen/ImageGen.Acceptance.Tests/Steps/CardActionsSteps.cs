using System.Text.Json;
using ImageGen.Acceptance.Tests.Support;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Storage;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Xunit;

namespace ImageGen.Acceptance.Tests.Steps;

[Binding]
public sealed class CardActionsSteps(ScenarioContext scenario)
{
    private AppWorld World => scenario.Get<AppWorld>();

    private ChatPageModel Chat => Assert.IsType<ChatPageModel>(Assert.IsType<ChatView>(World.Window.PageArea.Content).DataContext);

    [When("the edit prompt dialog will return {string}")]
    public void WhenEditPromptDialogWillReturn(string prompt) => World.Platform.QueuePrompt(prompt);

    [Then("the generation for model {string} has finished")]
    public Task ThenGenerationHasFinished(string modelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.WaitUntilAsync(() => Chat.Items.OfType<RunItemViewModel>()
            .SelectMany(run => run.Generations).Any(card => card.ModelId == modelId && card.IsDone),
            $"model '{modelId}' generation to finish", cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the generation for model {string} has failed")]
    public Task ThenGenerationHasFailed(string modelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.WaitUntilAsync(() => Chat.Items.OfType<RunItemViewModel>()
            .SelectMany(run => run.Generations).Any(card => card.ModelId == modelId && card.IsFailed),
            $"model '{modelId}' generation to fail", cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user opens error details for model {string}")]
    public Task WhenUserOpensErrorDetails(string modelId) => HeadlessApp.RunAsync(async _ =>
    {
        var card = Assert.Single(Chat.Items.OfType<RunItemViewModel>()
            .SelectMany(run => run.Generations), item => item.ModelId == modelId && item.IsFailed);
        await card.DetailsCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the error dialog contains {string}")]
    public void ThenErrorDialogContains(string text) => Assert.Contains(World.Platform.Alerts, alert =>
        alert.Message.Contains(text, StringComparison.Ordinal));

    [Then("the failed generation shows a short error reason")]
    public Task ThenFailedGenerationShowsShortReason() => HeadlessApp.RunAsync(_ =>
    {
        var card = Assert.Single(Chat.Items.OfType<RunItemViewModel>()
            .SelectMany(run => run.Generations), item => item.IsFailed);
        Assert.Equal(AppResources.ErrorKindUnknown, card.ErrorText);
        Assert.DoesNotContain("specific provider failure", card.ErrorText, StringComparison.Ordinal);
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [When("the user opens other models for the failed generation")]
    public Task WhenUserOpensOtherModels() => HeadlessApp.RunAsync(async _ =>
    {
        var card = Assert.Single(Chat.Items.OfType<RunItemViewModel>()
            .SelectMany(run => run.Generations), item => item.IsFailed);
        await card.OtherModelCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("model {string} is first in the used models")]
    public Task ThenModelIsFirstInUsedModels(string modelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.WaitUntilAsync(() => World.Window.PageArea.Content is ModelPickerView, "the model picker", cancellationToken);
        var picker = Assert.IsType<ModelPickerPageModel>(Assert.IsType<ModelPickerView>(World.Window.PageArea.Content).DataContext);
        await World.WaitUntilAsync(() => picker.UsedModels.Count > 0 && !picker.IsBusy, "used image models to load", cancellationToken);
        Assert.Equal(modelId, picker.UsedModels[0].Id);
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user chooses model {string} in the picker")]
    public Task WhenUserChoosesModelInPicker(string modelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var picker = Assert.IsType<ModelPickerPageModel>(Assert.IsType<ModelPickerView>(World.Window.PageArea.Content).DataContext);
        await World.WaitUntilAsync(() => picker.AllModels.Any(model => model.Id == modelId), $"model '{modelId}' in catalog", cancellationToken);
        await picker.PickCommand.ExecuteAsync(Assert.Single(picker.AllModels, model => model.Id == modelId));
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the timeline has two runs and the latest model is {string}")]
    public Task ThenTimelineHasTwoRunsAndLatestModel(string modelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.WaitUntilAsync(() => World.Window.PageArea.Content is ChatView
            && Chat.Items.OfType<RunItemViewModel>().Count() == 2
            && Chat.Items.OfType<RunItemViewModel>().Last().Generations.Any(card => card.ModelId == modelId && card.IsDone),
            "the selected model's run to finish", cancellationToken);
        Assert.Equal(modelId, Assert.Single(Chat.Items.OfType<RunItemViewModel>().Last().Generations).ModelId);
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user repeats the completed generation for model {string}")]
    public Task WhenUserRepeatsCompletedGeneration(string modelId) => HeadlessApp.RunAsync(async _ =>
    {
        var card = Assert.Single(Chat.Items.OfType<RunItemViewModel>()
            .SelectMany(run => run.Generations), item => item.ModelId == modelId && item.IsDone);
        await card.RepeatCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user requests a new seed for model {string}")]
    public Task WhenUserRequestsNewSeed(string modelId) => HeadlessApp.RunAsync(async _ =>
    {
        var card = Assert.Single(Chat.Items.OfType<RunItemViewModel>()
            .SelectMany(run => run.Generations), item => item.ModelId == modelId && item.IsDone);
        await card.NewSeedCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user edits the completed generation for model {string}")]
    public Task WhenUserEditsCompletedGeneration(string modelId) => HeadlessApp.RunAsync(async _ =>
    {
        var card = Assert.Single(Chat.Items.OfType<RunItemViewModel>()
            .SelectMany(run => run.Generations), item => item.ModelId == modelId && item.IsDone);
        await card.EditCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user copies the prompt for model {string}")]
    public Task WhenUserCopiesPrompt(string modelId) => HeadlessApp.RunAsync(async _ =>
    {
        var card = Assert.Single(Chat.Items.OfType<RunItemViewModel>()
            .SelectMany(run => run.Generations), item => item.ModelId == modelId && item.IsDone);
        await card.CopyPromptCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the clipboard contains {string}")]
    public void ThenClipboardContains(string text) => Assert.Equal(text, Assert.Single(World.Platform.ClipboardWrites));

    [When("the user saves the image for model {string}")]
    public Task WhenUserSavesImage(string modelId) => HeadlessApp.RunAsync(async _ =>
    {
        var card = Assert.Single(Chat.Items.OfType<RunItemViewModel>()
            .SelectMany(run => run.Generations), item => item.ModelId == modelId && item.IsDone && item.HasImage);
        await card.SaveCommand.ExecuteAsync(null);
        scenario.Set(card.Id, "actions.saved-generation-id");
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the save dialog suggests the generation filename")]
    public void ThenSaveDialogSuggestsGenerationFilename()
    {
        var id = scenario.Get<Guid>("actions.saved-generation-id");
        var save = Assert.Single(World.Platform.SaveRequests);
        Assert.Equal($"imagegen-{id.ToString("N")[..8]}.png", save.SuggestedFileName);
        Assert.True(File.Exists(save.SourcePath));
    }

    [When("the user has a generation card in state {string}")]
    public Task GivenUserHasGenerationCardInState(string state) => HeadlessApp.RunAsync(_ =>
    {
        var status = state switch
        {
            "done" => GenerationStatus.Succeeded,
            "failed" => GenerationStatus.Failed,
            "filtered" => GenerationStatus.Filtered,
            "cancelled" => GenerationStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown generation state."),
        };
        var sessionId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var generationId = Guid.NewGuid();
        var generation = new Generation
        {
            Id = generationId,
            RunId = runId,
            SessionId = sessionId,
            ProviderId = "openrouter",
            ModelId = "vendor/model",
            Prompt = "A small orange fox.",
            Seed = 7,
            SeedApplied = true,
            Status = status,
            ImagePath = status == GenerationStatus.Succeeded ? "fake/image.png" : null,
            CostUsd = status == GenerationStatus.Succeeded ? 0.01m : null,
            ErrorKind = status == GenerationStatus.Failed ? ErrorKind.Unknown : null,
            ErrorMessage = status == GenerationStatus.Failed ? "Provider detail." : null,
            CreatedAt = World.Time.GetUtcNow(),
            CompletedAt = World.Time.GetUtcNow(),
            Kind = MediaKind.Image,
            SortOrder = 0,
        };
        var provider = World.Provider;
        var card = new GenerationItemViewModel(
            generation,
            provider.GetRequiredService<IImageFileStore>(),
            World.Platform,
            World.Platform,
            World.Platform,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            (_, _) => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask);
        var run = new Run
        {
            Id = runId,
            SessionId = sessionId,
            Prompt = generation.Prompt,
            Origin = RunOrigin.Assistant,
            CreatedAt = generation.CreatedAt,
        };
        var copy = new CopyTextAction(World.Platform, provider.GetRequiredService<IMainThread>(), run.Prompt, TimeSpan.Zero);
        Chat.Items.Add(new RunItemViewModel(run, [card], copy));
        scenario.Set(card.Id, "actions.menu-generation-id");
        scenario.Set(state, "actions.menu-generation-state");
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [When("the user opens the generation actions menu")]
    public Task WhenUserOpensGenerationActionsMenu() => HeadlessApp.RunAsync(async _ =>
    {
        var id = scenario.Get<Guid>("actions.menu-generation-id");
        var card = Assert.Single(Chat.Items.OfType<RunItemViewModel>().SelectMany(run => run.Generations), item => item.Id == id);
        await card.ShowActionsCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the actions menu matches state {string}")]
    public void ThenActionsMenuMatchesState(string state)
    {
        var options = Assert.Single(World.Platform.ChooseRequests).Options;
        Assert.Contains(AppResources.Repeat, options);
        Assert.Contains(AppResources.NewSeed, options);
        Assert.Contains(AppResources.ActionEditPrompt, options);
        Assert.Contains(AppResources.CopyPrompt, options);
        Assert.Contains(AppResources.ActionDescribeImage, options);
        Assert.Contains(AppResources.ActionPromptFromImage, options);
        Assert.Equal(state == "done", options.Contains(World.Platform.ActionLabel, StringComparer.Ordinal));
        Assert.Equal(state == "done", options.Contains(AppResources.Compare, StringComparer.Ordinal));
        Assert.Equal(state is "failed" or "filtered", options.Contains(AppResources.OtherModel, StringComparer.Ordinal));
        Assert.Equal(state == "failed", options.Contains(AppResources.ErrorDetailsTitle, StringComparer.Ordinal));
    }

    [Then("the timeline has two completed runs for model {string}")]
    public Task ThenTimelineHasTwoCompletedRuns(string modelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.WaitUntilAsync(() => Chat.Items.OfType<RunItemViewModel>().Count(run =>
            run.Generations.Any(card => card.ModelId == modelId && card.IsDone)) == 2,
            "two completed runs in the timeline", cancellationToken);
        Assert.Equal(2, Chat.Items.OfType<RunItemViewModel>().Count());
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("both generations have the same prompt, model, and seed")]
    public Task ThenBothGenerationsHaveSamePromptModelAndSeed() => HeadlessApp.RunAsync(_ =>
    {
        var cards = Chat.Items.OfType<RunItemViewModel>().SelectMany(run => run.Generations).ToArray();
        Assert.Equal(2, cards.Length);
        Assert.Equal(cards[0].Prompt, cards[1].Prompt);
        Assert.Equal(cards[0].ModelId, cards[1].ModelId);
        Assert.Equal(cards[0].SeedText, cards[1].SeedText);

        var requests = World.Network.Requests.Where(request =>
            request.Method == "POST" && new Uri(request.Address).AbsolutePath == "/api/v1/images").ToArray();
        Assert.Equal(2, requests.Length);
        var bodies = requests.Select(request => JsonDocument.Parse(request.Body!)).ToArray();
        try
        {
            Assert.All(bodies, body => Assert.Equal(cards[0].ModelId, body.RootElement.GetProperty("model").GetString()));
            Assert.Equal(bodies[0].RootElement.GetProperty("prompt").GetString(), bodies[1].RootElement.GetProperty("prompt").GetString());
            Assert.Equal(bodies[0].RootElement.GetProperty("seed").GetInt64(), bodies[1].RootElement.GetProperty("seed").GetInt64());
        }
        finally
        {
            foreach (var body in bodies) body.Dispose();
        }

        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [Then("both generations have the same prompt and model but different seeds")]
    public Task ThenBothGenerationsHaveSamePromptAndModelButDifferentSeeds() => HeadlessApp.RunAsync(_ =>
    {
        var cards = Chat.Items.OfType<RunItemViewModel>().SelectMany(run => run.Generations).ToArray();
        Assert.Equal(2, cards.Length);
        Assert.Equal(cards[0].Prompt, cards[1].Prompt);
        Assert.Equal(cards[0].ModelId, cards[1].ModelId);
        Assert.NotEqual(cards[0].SeedText, cards[1].SeedText);

        var requests = World.Network.Requests.Where(request =>
            request.Method == "POST" && new Uri(request.Address).AbsolutePath == "/api/v1/images").ToArray();
        Assert.Equal(2, requests.Length);
        using var first = JsonDocument.Parse(requests[0].Body!);
        using var second = JsonDocument.Parse(requests[1].Body!);
        Assert.Equal(first.RootElement.GetProperty("prompt").GetString(), second.RootElement.GetProperty("prompt").GetString());
        Assert.Equal(first.RootElement.GetProperty("model").GetString(), second.RootElement.GetProperty("model").GetString());
        Assert.NotEqual(first.RootElement.GetProperty("seed").GetInt64(), second.RootElement.GetProperty("seed").GetInt64());
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [Then("the latest generation prompt is {string}")]
    public Task ThenLatestGenerationPromptIs(string prompt) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.WaitUntilAsync(() => Chat.Items.OfType<RunItemViewModel>().Count() == 2
            && Chat.Items.OfType<RunItemViewModel>().Last().Generations.Any(card => card.IsDone),
            "the edited prompt generation to finish", cancellationToken);
        var last = Assert.Single(Chat.Items.OfType<RunItemViewModel>().Last().Generations);
        Assert.Equal(prompt, last.Prompt);

        var requests = World.Network.Requests.Where(request =>
            request.Method == "POST" && new Uri(request.Address).AbsolutePath == "/api/v1/images").ToArray();
        using var latest = JsonDocument.Parse(requests[^1].Body!);
        Assert.Equal(prompt, latest.RootElement.GetProperty("prompt").GetString());
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the assistant was called once")]
    public void ThenAssistantWasCalledOnce() => Assert.Single(World.Network.Requests, request =>
        request.Method == "POST" && new Uri(request.Address).AbsolutePath == "/api/v1/chat/completions");
}

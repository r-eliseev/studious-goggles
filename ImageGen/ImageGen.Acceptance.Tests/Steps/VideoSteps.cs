using System.Net;
using System.Text.Json.Nodes;
using ImageGen.Acceptance.Tests.Support;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Domain.Models;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Xunit;

namespace ImageGen.Acceptance.Tests.Steps;

[Binding]
public sealed class VideoSteps(ScenarioContext scenario)
{
    private AppWorld World => scenario.Get<AppWorld>();

    private ChatPageModel Chat => Assert.IsType<ChatPageModel>(Assert.IsType<ChatView>(World.Window.PageArea.Content).DataContext);

    [Given("the OpenRouter video model catalog:")]
    public void GivenVideoModelCatalog(DataTable table) => World.OpenRouter.SetVideoModels(table.Rows.Select(row => new VideoModel
    {
        ProviderId = "openrouter",
        Id = row["id"],
        Name = row["name"],
        Durations = ParseInts(row["durations"]),
        Resolutions = ParseStrings(row["resolutions"]),
        AspectRatios = ParseStrings(row["aspect ratios"]),
        FrameImages = [],
        PriceSkus = new Dictionary<string, decimal>(),
        FetchedAt = World.Time.GetUtcNow(),
    }));

    [Given("the OpenRouter audio model catalog contains speech model {string}")]
    public void GivenAudioModelCatalog(string modelId) => World.OpenRouter.SetAudioModels([
        new AudioModel
        {
            ProviderId = "openrouter",
            Id = modelId,
            Name = "Test audio",
            Output = AudioOutput.Speech,
            FetchedAt = World.Time.GetUtcNow(),
        },
    ]);

    [Given("video content downloads fail with HTTP {int}")]
    public void GivenVideoContentDownloadFails(int statusCode) => World.OpenRouter.SetVideoContentError((HttpStatusCode)statusCode);

    [When("the user selects video model {string}")]
    public Task WhenUserSelectsVideoModel(string modelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var chat = Chat;
        await chat.OpenModelsCommand.ExecuteAsync(null);
        var page = chat.ModelsPane ?? Assert.IsType<SessionModelsPageModel>(Assert.IsType<SessionModelsView>(World.Window.PageArea.Content).DataContext);
        await World.WaitUntilAsync(() => !page.IsBusy && page.VideoModels.Count > 0, "video models to load", cancellationToken);
        page.SelectedTab = SessionModelsTab.VideoModels;
        Assert.Single(page.VideoModels, model => model.Reference.ModelId == modelId).IsSelected = true;
        await page.SaveCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user selects audio model {string}")]
    public Task WhenUserSelectsAudioModel(string modelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var chat = Chat;
        await chat.OpenModelsCommand.ExecuteAsync(null);
        var page = chat.ModelsPane ?? Assert.IsType<SessionModelsPageModel>(Assert.IsType<SessionModelsView>(World.Window.PageArea.Content).DataContext);
        await World.WaitUntilAsync(() => !page.IsBusy && page.SpeechModels.Count > 0, "audio models to load", cancellationToken);
        page.SelectedTab = SessionModelsTab.AudioModels;
        Assert.Single(page.SpeechModels, model => model.Reference.ModelId == modelId).IsSelected = true;
        await page.SaveCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the last assistant request does not include the {string} tool")]
    public Task ThenLastAssistantRequestDoesNotIncludeTool(string toolName) => AssertToolRequestAsync(toolName, expected: false);

    [Then("the assistant request includes the {string} tool")]
    public Task ThenAssistantRequestIncludesTool(string toolName) => AssertToolRequestAsync(toolName, expected: true);

    [Then("model {string} has an active video card")]
    public Task ThenModelHasActiveVideoCard(string modelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.WaitUntilAsync(() => Chat.Items.OfType<RunItemViewModel>().SelectMany(run => run.Generations)
            .Any(card => card.ModelId == modelId && card.IsVideo && card.IsActive), "an active video generation", cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the provider completes video generation")]
    public Task WhenProviderCompletesVideoGeneration() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.WaitUntilAsync(() => World.Network.Requests.Any(request => request.Method == "GET"
            && request.Address.Contains("/api/v1/videos/acceptance-video-", StringComparison.Ordinal)), "the video job to be polled", cancellationToken);
        await World.WaitUntilAsync(() => World.Delay.DelayCount > 0, "video polling to schedule its next delay", cancellationToken);
        var pollsBeforeAdvance = World.Network.Requests.Count(request => request.Method == "GET"
            && request.Address.Contains("/api/v1/videos/acceptance-video-", StringComparison.Ordinal));
        World.OpenRouter.CompleteVideoJobs();
        World.Time.Advance(TimeSpan.FromMinutes(1));
        await World.WaitUntilAsync(() => World.Network.Requests.Count(request => request.Method == "GET"
            && request.Address.Contains("/api/v1/videos/acceptance-video-", StringComparison.Ordinal)) > pollsBeforeAdvance,
            "the completed video job to be polled", cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("model {string} has a done video card with a saved file")]
    public Task ThenModelHasDoneVideoCard(string modelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var card = Assert.Single(Chat.Items.OfType<RunItemViewModel>().SelectMany(run => run.Generations), item => item.ModelId == modelId);
        await World.WaitUntilAsync(() => card.IsVideo && card.IsDone && card.HasMedia, "a completed video card", cancellationToken);
        Assert.True(File.Exists(card.VideoFilePath));
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("model {string} has failed without a video file")]
    public Task ThenModelHasFailedWithoutVideoFile(string modelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.WaitUntilAsync(() => Chat.Items.OfType<RunItemViewModel>().SelectMany(run => run.Generations)
            .Any(card => card.ModelId == modelId && card.IsVideo && card.IsFailed), "the failed video card", cancellationToken);
        var card = Assert.Single(Chat.Items.OfType<RunItemViewModel>().SelectMany(run => run.Generations), item => item.ModelId == modelId);
        Assert.False(card.HasMedia);
        Assert.False(File.Exists(card.VideoFilePath));
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user cancels the video generation")]
    public Task WhenUserCancelsVideoGeneration() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.WaitUntilAsync(() => Chat.Items.OfType<RunItemViewModel>().SelectMany(run => run.Generations)
            .Any(card => card.IsVideo && card.IsActive), "the active video card", cancellationToken);
        var card = Assert.Single(Chat.Items.OfType<RunItemViewModel>().SelectMany(run => run.Generations), item => item.IsVideo);
        await card.CancelCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the video card is cancelled with a provider may continue warning")]
    public Task ThenVideoCardCancelledWithWarning() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var card = Assert.Single(Chat.Items.OfType<RunItemViewModel>().SelectMany(run => run.Generations), item => item.IsVideo);
        await World.WaitUntilAsync(() => card.IsCancelled && card.HasCancellationStatus, "the cancellation warning", cancellationToken);
        Assert.Equal(AppResources.StoppedWaitingMedia, card.CancellationStatusText);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("model {string} has a done audio card with a saved file")]
    public Task ThenModelHasDoneAudioCard(string modelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.WaitUntilAsync(() => Chat.Items.OfType<RunItemViewModel>().SelectMany(run => run.Generations)
            .Any(card => card.ModelId == modelId && card.IsAudio), "an audio card", cancellationToken);
        var card = Assert.Single(Chat.Items.OfType<RunItemViewModel>().SelectMany(run => run.Generations), item => item.ModelId == modelId);
        await World.WaitUntilAsync(() => card.IsFinished, "audio generation to finish", cancellationToken);
        Assert.True(card.IsDone, card.ErrorText);
        Assert.True(card.HasMedia);
        Assert.True(File.Exists(card.AudioFilePath));
        return true;
    }, TestContext.Current.CancellationToken);

    private Task AssertToolRequestAsync(string toolName, bool expected) => HeadlessApp.RunAsync(_ =>
    {
        var request = World.Network.Requests.Last(item => item.Method == "POST"
            && item.Address.Contains("/api/v1/chat/completions", StringComparison.Ordinal)
            && JsonNode.Parse(item.Body!)?["tools"] is JsonArray);
        var tools = JsonNode.Parse(request.Body!)?["tools"]?.AsArray() ?? [];
        Assert.Equal(expected, tools.Any(tool => tool?["function"]?["name"]?.GetValue<string>() == toolName));
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    private static IReadOnlyList<int> ParseInts(string values) => values.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();

    private static IReadOnlyList<string> ParseStrings(string values) => values.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}

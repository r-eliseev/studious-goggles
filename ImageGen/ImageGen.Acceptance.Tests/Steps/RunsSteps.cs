using System.Globalization;
using System.Text.Json;
using Avalonia.Controls;
using ImageGen.Acceptance.Tests.Support;
using ImageGen.Avalonia;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Storage;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Xunit;

namespace ImageGen.Acceptance.Tests.Steps;

[Binding]
public sealed class RunsSteps(ScenarioContext scenario)
{
    private AppWorld World => scenario.Get<AppWorld>();

    private ChatPageModel Chat => Assert.IsType<ChatPageModel>(Assert.IsType<ChatView>(World.Window.PageArea.Content).DataContext);

    [Given("the current culture is {string}")]
    public void GivenCurrentCultureIs(string cultureName) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

    [Then("the user opens the generated image session")]
    public Task WhenTheUserOpensGeneratedImageSession() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var sessions = World.Provider.GetRequiredService<ImageGen.Presentation.PageModels.SessionsPageModel>();
        await World.WaitUntilAsync(() => sessions.Sessions.Count > 0, "the generated session row to load", cancellationToken);
        await sessions.Sessions[0].OpenCommand.ExecuteAsync(null);
        var navigation = World.Provider.GetRequiredService<AvaloniaNavigationService>();
        await World.WaitUntilAsync(() => navigation.CurrentRoute?.StartsWith(Routes.Chat + "?", StringComparison.Ordinal) == true,
            "the generated image session chat to open", cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the generated image is saved before the card shows it")]
    public Task ThenGeneratedImageIsSavedBeforeCardShowsIt() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        bool HasSavedImage() => Chat.Items.OfType<RunItemViewModel>()
            .SelectMany(run => run.Generations)
            .Any(card => card.IsDone && card.HasImage && File.Exists(card.ImageFilePath));

        await World.WaitUntilAsync(HasSavedImage, "the generated image file to exist before it is shown", cancellationToken);
        var card = Assert.Single(Chat.Items.OfType<RunItemViewModel>().SelectMany(run => run.Generations));
        Assert.True(File.Exists(card.ImageFilePath));
        Assert.NotEmpty(await World.Provider.GetRequiredService<IImageFileStore>().ReadAsync(card.RelativeImagePath, cancellationToken));
        scenario.Set(card.Id, "runs.saved-image-id");
        scenario.Set(card.ImageFilePath, "runs.saved-image-path");
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the generated image card still shows the same saved file")]
    public Task ThenGeneratedImageCardStillShowsSameSavedFile() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var imageId = scenario.Get<Guid>("runs.saved-image-id");
        var filePath = scenario.Get<string>("runs.saved-image-path");
        bool HasRestoredImage() => Chat.Items.OfType<RunItemViewModel>()
            .SelectMany(run => run.Generations)
            .Any(card => card.Id == imageId && card.IsDone && card.HasImage);

        await World.WaitUntilAsync(HasRestoredImage, "the saved image card to be restored", cancellationToken);
        var card = Assert.Single(Chat.Items.OfType<RunItemViewModel>().SelectMany(run => run.Generations), item => item.Id == imageId);
        Assert.True(card.HasImage);
        Assert.Equal(filePath, card.ImageFilePath);
        Assert.True(File.Exists(filePath));
        Assert.NotEmpty(await World.Provider.GetRequiredService<IImageFileStore>().ReadAsync(card.RelativeImagePath, cancellationToken));
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the provider completes generation for model {string}")]
    public void WhenTheProviderCompletesGenerationForModel(string modelId) => World.OpenRouter.CompleteHeldGeneration(modelId);

    [Then("model {string} has an active generation card")]
    public Task ThenModelHasAnActiveGenerationCard(string modelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        bool HasActiveCard() => Chat.Items.OfType<RunItemViewModel>()
            .SelectMany(run => run.Generations)
            .Any(generation => generation.ModelId == modelId && generation.IsActive);

        await World.WaitUntilAsync(HasActiveCard, $"model '{modelId}' to have an active generation card", cancellationToken);
        var card = Assert.Single(Chat.Items.OfType<RunItemViewModel>().SelectMany(run => run.Generations), generation => generation.ModelId == modelId);
        Assert.True(card.IsActive);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("model {string} has a done generation card with an image")]
    public Task ThenModelHasADoneGenerationCardWithAnImage(string modelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        bool HasDoneCard() => Chat.Items.OfType<RunItemViewModel>()
            .SelectMany(run => run.Generations)
            .Any(generation => generation.ModelId == modelId && generation.IsDone && generation.HasImage);

        await World.WaitUntilAsync(HasDoneCard, $"model '{modelId}' to have a completed image card", cancellationToken);
        var card = Assert.Single(Chat.Items.OfType<RunItemViewModel>().SelectMany(run => run.Generations), generation => generation.ModelId == modelId);
        Assert.True(card.IsDone);
        Assert.True(card.HasImage);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the generation card shows model {string}, seed, and cost {string}")]
    public Task ThenGenerationCardShowsModelSeedAndCost(string modelId, string costText) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        bool HasExpectedCard() => Chat.Items.OfType<RunItemViewModel>()
            .SelectMany(run => run.Generations)
            .Any(card => card.ModelId == modelId && card.IsDone && card.HasImage && card.CostText == costText);

        await World.WaitUntilAsync(HasExpectedCard, "the completed card to show provider cost with invariant formatting", cancellationToken);
        var card = Assert.Single(Chat.Items.OfType<RunItemViewModel>().SelectMany(run => run.Generations));
        Assert.Equal(modelId, card.ModelId);
        Assert.StartsWith("seed ", card.SeedText, StringComparison.Ordinal);
        Assert.Equal(costText, card.CostText);
        Assert.Contains(costText, card.DetailsText, StringComparison.Ordinal);
        return true;
    }, TestContext.Current.CancellationToken);

    [Given("the seed source returns:")]
    public void GivenTheSeedSourceReturns(DataTable table) =>
        World.Seeds.SetSequence(table.Rows.Select(row => long.Parse(row["seed"], System.Globalization.CultureInfo.InvariantCulture)));

    [When("the user selects these image models with two runs per model:")]
    public Task WhenTheUserSelectsTheseImageModelsWithTwoRunsPerModel(DataTable table)
    {
        var modelIds = table.Rows.Select(row => row["model id"]).ToHashSet(StringComparer.Ordinal);
        scenario.Set(modelIds, "runs.selected-models");

        return HeadlessApp.RunAsync(async cancellationToken =>
        {
            var chat = Chat;
            await chat.OpenModelsCommand.ExecuteAsync(null);
            var page = chat.ModelsPane;
            if (page is null)
            {
                await World.WaitUntilAsync(
                    () => World.Window.PageArea.Content is SessionModelsView,
                    "the session models page to open",
                    cancellationToken);
                page = Assert.IsType<SessionModelsPageModel>(Assert.IsType<SessionModelsView>(World.Window.PageArea.Content).DataContext);
            }

            await World.WaitUntilAsync(
                () => page.Models.Count > 0 && !page.IsBusy,
                "the image model rows to finish loading",
                cancellationToken);
            Assert.Equal(modelIds.Count, page.Models.Count(model => modelIds.Contains(model.Id)));
            foreach (var model in page.Models)
            {
                model.IsSelected = modelIds.Contains(model.Id);
            }

            page.RunsPerModel = 2;
            Assert.True(page.SaveCommand.CanExecute(null), $"The models page is still busy: {page.IsBusy}.");
            await page.SaveCommand.ExecuteAsync(null);
            Assert.True(
                World.Window.PageArea.Content is ChatView,
                $"The models page did not close; route={World.Provider.GetRequiredService<AvaloniaNavigationService>().CurrentRoute}; alerts={string.Join(" | ", World.Platform.Alerts.Select(alert => alert.Message))}.");
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [When("the user selects image models for this run:")]
    public Task WhenTheUserSelectsImageModelsForThisRun(DataTable table)
    {
        var modelIds = table.Rows.Select(row => row["model id"]).ToHashSet(StringComparer.Ordinal);
        return SelectModelsAsync(modelIds, runsPerModel: 1);
    }

    [When("the next assistant response is:")]
    public void WhenTheNextAssistantResponseIs(DataTable table)
    {
        var row = Assert.Single(table.Rows);
        Assert.Equal("generate_images", row["response"]);
        World.OpenRouter.QueueGenerateImages(row["prompt"]);
    }

    [Then("the run summary shows {string}")]
    public Task ThenTheRunSummaryShows(string completionText) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        bool HasExpectedSummary() => Chat.Items.OfType<RunItemViewModel>().Any(run => run.HeaderText.Contains(completionText, StringComparison.Ordinal));
        await World.WaitUntilAsync(HasExpectedSummary, $"the run summary '{completionText}'", cancellationToken);
        var run = Assert.Single(Chat.Items.OfType<RunItemViewModel>());
        Assert.Contains(completionText, run.HeaderText, StringComparison.Ordinal);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the generation card shows image dimensions {string}")]
    public Task ThenGenerationCardShowsImageDimensions(string dimensionsText) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        bool HasDimensions() => Chat.Items.OfType<RunItemViewModel>()
            .SelectMany(run => run.Generations)
            .Any(card => card.IsDone && card.HasImage && card.ImageDimensionsText == dimensionsText);

        await World.WaitUntilAsync(HasDimensions, $"the image card dimensions '{dimensionsText}'", cancellationToken);
        var card = Assert.Single(Chat.Items.OfType<RunItemViewModel>().SelectMany(run => run.Generations));
        Assert.Equal(dimensionsText, card.ImageDimensionsText);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the current run has two cards for models {string} and {string}")]
    public Task ThenCurrentRunHasTwoCardsForModels(string firstModelId, string secondModelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.WaitUntilAsync(() => Chat.Items.OfType<RunItemViewModel>().Any(), "the current run to appear", cancellationToken);
        var run = Assert.Single(Chat.Items.OfType<RunItemViewModel>());
        Assert.Equal(2, run.Generations.Count);
        Assert.Equal(new[] { firstModelId, secondModelId }.Order(StringComparer.Ordinal), run.Generations.Select(card => card.ModelId).Order(StringComparer.Ordinal));
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the next run has one done card for model {string}")]
    public Task ThenNextRunHasOneDoneCardForModel(string modelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        bool HasSecondRun() => Chat.Items.OfType<RunItemViewModel>().Count() == 2
            && Chat.Items.OfType<RunItemViewModel>().Last().Generations is [{ IsDone: true, HasImage: true } card]
            && card.ModelId == modelId;

        await World.WaitUntilAsync(HasSecondRun, $"the second run to finish with only model '{modelId}'", cancellationToken);
        var runs = Chat.Items.OfType<RunItemViewModel>().ToArray();
        Assert.Equal(2, runs.Length);
        Assert.Equal(2, runs[0].Generations.Count);
        var second = Assert.Single(runs[1].Generations);
        Assert.Equal(modelId, second.ModelId);
        Assert.True(second.IsDone);
        Assert.True(second.HasImage);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the next run estimate is {string}")]
    public Task ThenNextRunEstimateIs(string amount) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.WaitUntilAsync(() => Chat.HasNextRunEstimate, "the next run estimate to load", cancellationToken);
        Assert.Contains(amount, Chat.NextRunEstimateText, StringComparison.Ordinal);
        return true;
    }, TestContext.Current.CancellationToken);

    private Task SelectModelsAsync(HashSet<string> modelIds, int runsPerModel)
    {
        scenario.Set(modelIds, "runs.selected-models");
        return HeadlessApp.RunAsync(async cancellationToken =>
        {
            var chat = Chat;
            await chat.OpenModelsCommand.ExecuteAsync(null);
            var page = chat.ModelsPane;
            if (page is null)
            {
                await World.WaitUntilAsync(
                    () => World.Window.PageArea.Content is SessionModelsView,
                    "the session models page to open",
                    cancellationToken);
                page = Assert.IsType<SessionModelsPageModel>(Assert.IsType<SessionModelsView>(World.Window.PageArea.Content).DataContext);
            }

            await World.WaitUntilAsync(() => page.Models.Count > 0 && !page.IsBusy, "the image model rows to finish loading", cancellationToken);
            Assert.Equal(modelIds.Count, page.Models.Count(model => modelIds.Contains(model.Id)));
            foreach (var model in page.Models) model.IsSelected = modelIds.Contains(model.Id);
            page.RunsPerModel = runsPerModel;
            Assert.True(page.SaveCommand.CanExecute(null), $"The models page is still busy: {page.IsBusy}.");
            await page.SaveCommand.ExecuteAsync(null);
            Assert.True(World.Window.PageArea.Content is ChatView);
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Then("the run shows four generation cards with seeds {int} and {int} for each model")]
    public Task ThenTheRunShowsFourGenerationCardsWithSeedsForEachModel(long firstSeed, long secondSeed) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        bool HasFinishedRun() => Chat.Items.OfType<RunItemViewModel>().Any(run =>
            run.Generations.Count == 4 && run.Generations.All(generation => generation.IsDone));

        await World.WaitUntilAsync(HasFinishedRun, "the four image generations to finish", cancellationToken);
        var run = Assert.Single(Chat.Items.OfType<RunItemViewModel>());
        Assert.Equal(4, run.Generations.Count);

        bool HasFourImageRequests() => World.Network.Requests.Count(IsImageRequest) == 4;
        await World.WaitUntilAsync(HasFourImageRequests, "all four image requests to be recorded", cancellationToken);
        var requests = World.Network.Requests.Where(IsImageRequest).ToArray();
        var expectedModels = scenario.Get<HashSet<string>>("runs.selected-models");
        Assert.Equal(expectedModels.Order(StringComparer.Ordinal), requests
            .Select(request => ReadString(request.Body!, "model"))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal));

        foreach (var modelId in expectedModels)
        {
            var seeds = requests
                .Where(request => ReadString(request.Body!, "model") == modelId)
                .Select(request => ReadInt64(request.Body!, "seed"))
                .Order()
                .ToArray();
            Assert.Equal(new[] { firstSeed, secondSeed }.Order(), seeds);
        }

        return true;
    }, TestContext.Current.CancellationToken);

    [Then("model {string} receives requests without a seed and the cards show seed not supported")]
    public Task ThenModelReceivesRequestsWithoutASeedAndCardsShowSeedNotSupported(string modelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        bool HasTwoFinishedCards() => Chat.Items.OfType<RunItemViewModel>().Any(run =>
            run.Generations.Count == 2 && run.Generations.All(generation => generation.IsDone));

        await World.WaitUntilAsync(HasTwoFinishedCards, "the two generations to finish", cancellationToken);
        var run = Assert.Single(Chat.Items.OfType<RunItemViewModel>());
        var cards = run.Generations.Where(generation => generation.ModelId == modelId).ToArray();
        Assert.Equal(2, cards.Length);
        Assert.All(cards, card => Assert.Equal(AppResources.SeedNotSupported, card.SeedText));

        var requests = World.Network.Requests.Where(request =>
            IsImageRequest(request) && ReadString(request.Body!, "model") == modelId).ToArray();
        Assert.Equal(2, requests.Length);
        foreach (var request in requests)
        {
            using var body = JsonDocument.Parse(request.Body!);
            Assert.False(body.RootElement.TryGetProperty("seed", out _));
        }

        return true;
    }, TestContext.Current.CancellationToken);

    [Then("model {string} generations are failed and model {string} generations are done")]
    public Task ThenModelGenerationsAreFailedAndModelGenerationsAreDone(string failedModelId, string doneModelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        bool HasFinishedRun() => Chat.Items.OfType<RunItemViewModel>().Any(run =>
            run.Generations.Count == 4 && run.Generations.All(generation => generation.IsFailed || generation.IsDone));

        await World.WaitUntilAsync(HasFinishedRun, "all model generations to finish", cancellationToken);
        var run = Assert.Single(Chat.Items.OfType<RunItemViewModel>());
        var failed = run.Generations.Where(generation => generation.ModelId == failedModelId).ToArray();
        var done = run.Generations.Where(generation => generation.ModelId == doneModelId).ToArray();
        Assert.Equal(2, failed.Length);
        Assert.Equal(2, done.Length);
        Assert.All(failed, generation =>
        {
            Assert.Equal(GenerationStatusKind.Failed, generation.StatusKind);
            Assert.Equal(AppResources.StatusFailed, generation.StatusText);
            Assert.Equal(AppResources.ErrorKindUnknown, generation.ErrorText);
        });
        Assert.All(done, generation =>
        {
            Assert.Equal(GenerationStatusKind.Done, generation.StatusKind);
            Assert.Equal(AppResources.StatusSucceeded, generation.StatusText);
            Assert.True(generation.HasImage);
        });
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("model {string} generations are filtered and model {string} generations are done")]
    public Task ThenModelGenerationsAreFilteredAndModelGenerationsAreDone(string filteredModelId, string doneModelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        bool HasTerminalRun() => Chat.Items.OfType<RunItemViewModel>().Any(run =>
            run.Generations.Count == 4 && run.Generations.All(generation => generation.IsDone || generation.IsFailed || generation.IsFiltered));

        await World.WaitUntilAsync(HasTerminalRun, "all filtered and successful generations to finish", cancellationToken);
        var run = Assert.Single(Chat.Items.OfType<RunItemViewModel>());
        var filtered = run.Generations.Where(generation => generation.ModelId == filteredModelId).ToArray();
        var done = run.Generations.Where(generation => generation.ModelId == doneModelId).ToArray();
        Assert.Equal(2, filtered.Length);
        Assert.Equal(2, done.Length);
        Assert.All(filtered, generation =>
        {
            Assert.True(generation.IsFiltered);
            Assert.False(generation.IsFailed);
            Assert.Equal(GenerationStatusKind.Filtered, generation.StatusKind);
            Assert.Equal(AppResources.StatusFiltered, generation.StatusText);
        });
        Assert.All(done, generation =>
        {
            Assert.Equal(GenerationStatusKind.Done, generation.StatusKind);
            Assert.Equal(AppResources.StatusSucceeded, generation.StatusText);
            Assert.True(generation.HasImage);
        });
        return true;
    }, TestContext.Current.CancellationToken);

    private static bool IsImageRequest(FakeNetwork.RecordedRequest request) =>
        request.Method == "POST" && new Uri(request.Address).AbsolutePath == "/api/v1/images";

    private static string ReadString(string body, string property) =>
        JsonDocument.Parse(body).RootElement.GetProperty(property).GetString()!;

    private static long ReadInt64(string body, string property) =>
        JsonDocument.Parse(body).RootElement.GetProperty(property).GetInt64();
}

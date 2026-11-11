using System.Text.Json;
using Avalonia.Controls;
using ImageGen.Acceptance.Tests.Support;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Views;
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

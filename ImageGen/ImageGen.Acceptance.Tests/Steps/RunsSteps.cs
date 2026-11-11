using System.Text.Json;
using Avalonia.Controls;
using ImageGen.Acceptance.Tests.Support;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Views;
using ImageGen.Presentation.PageModels;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Xunit;

namespace ImageGen.Acceptance.Tests.Steps;

[Binding]
public sealed class RunsSteps(ScenarioContext scenario)
{
    private AppWorld World => scenario.Get<AppWorld>();

    private ChatPageModel Chat => Assert.IsType<ChatPageModel>(Assert.IsType<ChatView>(World.Window.PageArea.Content).DataContext);

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

    private static bool IsImageRequest(FakeNetwork.RecordedRequest request) =>
        request.Method == "POST" && new Uri(request.Address).AbsolutePath == "/api/v1/images";

    private static string ReadString(string body, string property) =>
        JsonDocument.Parse(body).RootElement.GetProperty(property).GetString()!;

    private static long ReadInt64(string body, string property) =>
        JsonDocument.Parse(body).RootElement.GetProperty(property).GetInt64();
}

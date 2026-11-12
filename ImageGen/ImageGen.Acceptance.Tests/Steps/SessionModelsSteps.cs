using System.Text.Json.Nodes;
using ImageGen.Acceptance.Tests.Support;
using ImageGen.Avalonia.Views;
using ImageGen.Presentation.PageModels;
using Reqnroll;
using Xunit;

namespace ImageGen.Acceptance.Tests.Steps;

[Binding]
public sealed class SessionModelsSteps(ScenarioContext scenario)
{
    private AppWorld World => scenario.Get<AppWorld>();

    [When("the user opens session models")]
    public Task WhenTheUserOpensSessionModels() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await OpenModelsAsync(cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user selects models {string} and {string} with {int} runs per model")]
    public Task WhenTheUserSelectsModelsAndRunsPerModel(string firstId, string secondId, int runs) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var page = await OpenModelsAsync(cancellationToken);
        var selectedIds = new HashSet<string>([firstId, secondId], StringComparer.Ordinal);
        Assert.Equal(selectedIds.Count, page.Models.Count(model => selectedIds.Contains(model.Id)));
        foreach (var model in page.Models) model.IsSelected = selectedIds.Contains(model.Id);
        page.RunsPerModel = runs;
        await page.SaveCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the run shows six done cards for the two selected models")]
    public Task ThenTheRunShowsSixDoneCardsForTheTwoSelectedModels() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var chat = Assert.IsType<ChatPageModel>(Assert.IsType<ChatView>(World.Window.PageArea.Content).DataContext);
        await World.WaitUntilAsync(
            () => chat.Items.OfType<RunItemViewModel>().Any(run => run.Generations.Count == 6 && run.Generations.All(card => card.IsDone)),
            "six completed generations",
            cancellationToken);
        var run = Assert.Single(chat.Items.OfType<RunItemViewModel>());
        Assert.Equal(6, run.Generations.Count);
        Assert.Equal(3, run.Generations.Count(card => card.ModelId == "black-forest-labs/flux.2-klein-4b"));
        Assert.Equal(3, run.Generations.Count(card => card.ModelId == "vendor/alpha"));
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the runs control is limited to 1 through 10")]
    public Task ThenTheRunsControlIsLimitedToOneThroughTen() => HeadlessApp.RunAsync(_ =>
    {
        var page = CurrentModelsPage();
        Assert.Equal(1, page.MinRunsPerModel);
        Assert.Equal(10, page.MaxRunsPerModel);
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [When("the user snapshots model catalog requests")]
    public Task WhenTheUserSnapshotsModelCatalogRequests() => HeadlessApp.RunAsync(_ =>
    {
        scenario.Set(CatalogRequestCount(), "models.catalog-request-count");
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [Then("searching models for {string} shows only {string}")]
    public Task ThenSearchingModelsShowsOnly(string search, string modelId) => HeadlessApp.RunAsync(_ =>
    {
        var page = CurrentModelsPage();
        page.SearchText = search;
        Assert.Equal(modelId, Assert.Single(page.Models).Id);
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [Then("image model groups include {string}, {string}, and {string}")]
    public Task ThenImageModelGroupsInclude(string first, string second, string third) => HeadlessApp.RunAsync(_ =>
    {
        var vendors = CurrentModelsPage().ImageModelGroups.Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(first, vendors);
        Assert.Contains(second, vendors);
        Assert.Contains(third, vendors);
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [Then("model {string} shows price {string}")]
    public Task ThenModelShowsPrice(string modelId, string price) => HeadlessApp.RunAsync(_ =>
    {
        var model = Assert.Single(CurrentModelsPage().Models, item => item.Id == modelId);
        Assert.Equal(price, model.PriceText);
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [When("the user cancels model changes")]
    public Task WhenTheUserCancelsModelChanges() => HeadlessApp.RunAsync(async _ =>
    {
        await CurrentModelsPage().CancelCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user reopens session models")]
    public Task WhenTheUserReopensSessionModels() => WhenTheUserOpensSessionModels();

    [Then("reopening models does not request the catalog again")]
    public Task ThenReopeningModelsDoesNotRequestTheCatalogAgain() => HeadlessApp.RunAsync(_ =>
    {
        Assert.Equal(scenario.Get<int>("models.catalog-request-count"), CatalogRequestCount());
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [When("the user refreshes the model catalog")]
    public Task WhenTheUserRefreshesTheModelCatalog() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var page = CurrentModelsPage();
        scenario.Set(CatalogRequestCount(), "models.catalog-request-count");
        await page.RefreshCommand.ExecuteAsync(null);
        await World.WaitUntilAsync(() => !page.IsBusy && page.Models.Count > 0, "the refreshed image catalog", cancellationToken);
        Assert.True(CatalogRequestCount() > scenario.Get<int>("models.catalog-request-count"));
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user selects {string} and {int} runs per model")]
    public Task WhenTheUserSelectsModelAndRunCount(string modelId, int runs) => HeadlessApp.RunAsync(_ =>
    {
        var page = CurrentModelsPage();
        Assert.Single(page.Models, model => model.Id == modelId).IsSelected = true;
        page.RunsPerModel = runs;
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [Then("only the default image model is selected with 1 run per model")]
    public Task ThenOnlyTheDefaultImageModelIsSelectedWithOneRunPerModel() => HeadlessApp.RunAsync(_ =>
    {
        var page = CurrentModelsPage();
        Assert.Equal(1, page.RunsPerModel);
        Assert.Equal("black-forest-labs/flux.2-klein-4b", Assert.Single(page.Models, model => model.IsSelected).Id);
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [When("the user saves session parameters aspect ratio {string}, resolution {string}, and format {string}")]
    public Task WhenTheUserSavesSessionParameters(string aspectRatio, string resolution, string format) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        PrepareNarrowWindow();
        var chat = Assert.IsType<ChatPageModel>(Assert.IsType<ChatView>(World.Window.PageArea.Content).DataContext);
        await chat.OpenParametersCommand.ExecuteAsync(null);
        var page = await GetParametersPageAsync(chat, cancellationToken);
        await World.WaitUntilAsync(() => page.OutputFormatOptions.Count > 0, "session parameter options", cancellationToken);
        page.SelectedAspectRatio = Assert.Single(page.AspectRatioOptions, option => option.Value == aspectRatio);
        page.SelectedResolution = Assert.Single(page.ResolutionOptions, option => option.Value == resolution);
        page.SelectedOutputFormat = Assert.Single(page.OutputFormatOptions, option => option.Value == format);
        await page.SaveCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the image request contains aspect ratio {string}, resolution {string}, and format {string}")]
    public Task ThenImageRequestContainsParameters(string aspectRatio, string resolution, string format) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.WaitUntilAsync(() => World.Network.Requests.Any(IsImageRequest), "the image request", cancellationToken);
        var request = Assert.Single(World.Network.Requests, IsImageRequest);
        var body = JsonNode.Parse(request.Body!)!;
        Assert.Equal(aspectRatio, body["aspect_ratio"]?.GetValue<string>());
        Assert.Equal(resolution, body["resolution"]?.GetValue<string>());
        Assert.Equal(format, body["output_format"]?.GetValue<string>());
        return true;
    }, TestContext.Current.CancellationToken);

    private async Task<SessionModelsPageModel> OpenModelsAsync(CancellationToken cancellationToken)
    {
        PrepareNarrowWindow();
        var chat = Assert.IsType<ChatPageModel>(Assert.IsType<ChatView>(World.Window.PageArea.Content).DataContext);
        await chat.OpenModelsCommand.ExecuteAsync(null);
        if (chat.ModelsPane is { } pane)
        {
            await World.WaitUntilAsync(() => !pane.IsBusy && pane.Models.Count > 0, "session image models to load", cancellationToken);
            return pane;
        }

        await World.WaitUntilAsync(() => World.Window.PageArea.Content is SessionModelsView, "session models page", cancellationToken);
        var page = CurrentModelsPage();
        await World.WaitUntilAsync(() => !page.IsBusy && page.Models.Count > 0, "session image models to load", cancellationToken);
        return page;
    }

    private void PrepareNarrowWindow()
    {
        World.Window.Width = 800;
        World.Window.Height = 900;
        World.Window.Show();
        World.Window.UpdateLayout();
    }

    private async Task<SessionParametersPageModel> GetParametersPageAsync(ChatPageModel chat, CancellationToken cancellationToken)
    {
        if (chat.ParametersPane is { } pane)
        {
            await World.WaitUntilAsync(() => !pane.IsBusy && pane.OutputFormatOptions.Count > 0, "session parameters to load", cancellationToken);
            return pane;
        }

        await World.WaitUntilAsync(() => World.Window.PageArea.Content is SessionParametersView, "session parameters page", cancellationToken);
        var page = Assert.IsType<SessionParametersPageModel>(Assert.IsType<SessionParametersView>(World.Window.PageArea.Content).DataContext);
        await World.WaitUntilAsync(() => !page.IsBusy && page.OutputFormatOptions.Count > 0, "session parameters to load", cancellationToken);
        return page;
    }

    private SessionModelsPageModel CurrentModelsPage()
    {
        if (World.Window.PageArea.Content is ChatView { DataContext: ChatPageModel chat } && chat.ModelsPane is { } pane)
        {
            return pane;
        }

        return Assert.IsType<SessionModelsPageModel>(Assert.IsType<SessionModelsView>(World.Window.PageArea.Content).DataContext);
    }

    private int CatalogRequestCount() => World.Network.Requests.Count(request => request.Method == "GET"
        && request.Address.Contains("/api/v1/images/models", StringComparison.Ordinal));

    private static bool IsImageRequest(FakeNetwork.RecordedRequest request) => request.Method == "POST"
        && request.Address.Contains("/api/v1/images", StringComparison.Ordinal);
}

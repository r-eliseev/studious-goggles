using ImageGen.Acceptance.Tests.Support;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Search;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Xunit;

namespace ImageGen.Acceptance.Tests.Steps;

[Binding]
public sealed class SearchSteps(ScenarioContext scenario)
{
    private AppWorld World => scenario.Get<AppWorld>();

    private SessionsPageModel Sessions => World.Provider.GetRequiredService<SessionsPageModel>();

    private SettingsPageModel Settings => Assert.IsType<SettingsPageModel>(Assert.IsType<SettingsView>(World.Window.PageArea.Content).DataContext);

    [When("the user searches sessions for {string}")]
    public Task WhenUserSearchesSessionsFor(string query) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var page = Sessions;
        page.SearchText = query;
        World.Time.Advance(SessionsPageModel.SearchDebounce);
        await World.WaitUntilAsync(() => !page.IsSearching &&
            (page.SearchResults.Count > 0 || page.HasNoResults), "session search results", cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the search results include the session titled {string}")]
    public void ThenSearchResultsIncludeSession(string title) =>
        Assert.Contains(Sessions.SearchResults, session => session.Title == title);

    [Then("the search shows no results")]
    public void ThenSearchShowsNoResults()
    {
        Assert.True(Sessions.HasNoResults);
        Assert.Empty(Sessions.SearchResults);
    }

    [When("the user clears the session search")]
    public Task WhenUserClearsSessionSearch() => HeadlessApp.RunAsync(async _ =>
    {
        Sessions.ClearSearchCommand.Execute(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("session search is cleared and the session list is restored")]
    public Task ThenSearchClearedAndListRestored() => HeadlessApp.RunAsync(_ =>
    {
        Assert.False(Sessions.IsSearchActive);
        Assert.False(Sessions.HasNoResults);
        Assert.Empty(Sessions.SearchResults);
        Assert.Single(Sessions.Sessions);
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [Then("semantic search is off")]
    public Task ThenSemanticSearchIsOff() => HeadlessApp.RunAsync(_ =>
    {
        Assert.Equal(SemanticIndexState.Off, Sessions.SemanticIndexStatus.State);
        Assert.False(Sessions.SemanticIndexStatus.Enabled);
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [Then("the semantic index is off")]
    public Task ThenSettingsSemanticIndexIsOff() => HeadlessApp.RunAsync(_ =>
    {
        Assert.Equal(SemanticIndexState.Off, Settings.SemanticIndexStatus.State);
        Assert.False(Settings.SemanticIndexStatus.Enabled);
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [When("the user enables semantic search in Settings")]
    public Task WhenUserEnablesSemanticSearchInSettings() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await Sessions.OpenSettingsCommand.ExecuteAsync(null);
        var navigation = World.Provider.GetRequiredService<AvaloniaNavigationService>();
        await World.WaitUntilAsync(() => navigation.CurrentRoute == Routes.Settings, "Settings route", cancellationToken);
        var settings = Settings;
        if (settings.EmbeddingModels.Count == 0) await settings.LoadCommand.ExecuteAsync(null);
        Assert.False(settings.SemanticIndexStatus.Enabled);
        await settings.ToggleSemanticSearchCommand.ExecuteAsync(null);
        await World.WaitUntilAsync(() => settings.SemanticIndexStatus.State == SemanticIndexState.Ready,
            "the semantic history index to become ready", cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the semantic index is ready")]
    public Task ThenSemanticIndexIsReady() => HeadlessApp.RunAsync(_ =>
    {
        Assert.Equal(SemanticIndexState.Ready, Settings.SemanticIndexStatus.State);
        Assert.True(Settings.SemanticIndexStatus.Enabled);
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [When("the user changes the embedding model to {string}")]
    public Task WhenUserChangesEmbeddingModel(string modelId) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var settings = Settings;
        await World.WaitUntilAsync(() => settings.EmbeddingModels.Any(model => model.Id == modelId),
            $"embedding model '{modelId}' to load", cancellationToken);
        settings.SelectedEmbeddingModel = Assert.Single(settings.EmbeddingModels, model => model.Id == modelId);
        await settings.ChangeEmbeddingModelCommand.ExecuteAsync(null);
        await World.WaitUntilAsync(() => settings.SemanticIndexStatus.State == SemanticIndexState.Ready,
            "history to be reindexed with the selected embedding model", cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("embeddings were requested with model {string}")]
    public void ThenEmbeddingsRequestedWithModel(string modelId)
    {
        var requests = World.Network.Requests.Where(request =>
            request.Method == "POST" && new Uri(request.Address).AbsolutePath == "/api/v1/embeddings");
        Assert.Contains(requests, request =>
        {
            using var body = System.Text.Json.JsonDocument.Parse(request.Body!);
            return body.RootElement.GetProperty("model").GetString() == modelId;
        });
    }

    [Then("Settings report the embedding model is unavailable")]
    public Task ThenSettingsReportEmbeddingModelUnavailable() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var settings = Settings;
        await World.WaitUntilAsync(() => settings.EmbeddingModels.Count > 0, "embedding model status to load", cancellationToken);
        Assert.True(settings.IsEmbeddingModelUnavailable);
        Assert.Equal(SemanticIndexState.Unavailable, settings.SemanticIndexStatus.State);
        Assert.Contains("vendor/missing", settings.SemanticSearchStatusText, StringComparison.Ordinal);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the fake received embedding requests")]
    public void ThenFakeReceivedEmbeddingRequests() => Assert.Contains(World.Network.Requests, request =>
        request.Method == "POST" && new Uri(request.Address).AbsolutePath == "/api/v1/embeddings");

    [Given("the fake embeddings return these vectors:")]
    public void GivenFakeEmbeddingsReturnVectors(DataTable table) => World.OpenRouter.SetEmbeddingVectors(
        table.Rows.Select(row => new KeyValuePair<string, float[]>(row["text"], row["vector"].Split(',')
            .Select(value => float.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray())));

    [Given("the fake embedding model catalog:")]
    public void GivenFakeEmbeddingModelCatalog(DataTable table) => World.OpenRouter.SetEmbeddingModels(
        table.Rows.Select(row => new FakeOpenRouter.EmbeddingModelInfo(row["id"], row["name"])));

    [Given("semantic search is enabled with unavailable model {string}")]
    public async Task GivenSemanticSearchEnabledWithUnavailableModel(string modelId)
    {
        Directory.CreateDirectory(World.DataDirectory);
        await File.WriteAllTextAsync(Path.Combine(World.DataDirectory, "search.json"),
            System.Text.Json.JsonSerializer.Serialize(new { enabled = true, model = modelId }), TestContext.Current.CancellationToken);
    }

    [When("the user selects semantic search")]
    public Task WhenUserSelectsSemanticSearch() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var sessions = Sessions;
        await World.WaitUntilAsync(() => sessions.CanUseSemanticSearch, "the semantic index to be ready for search", cancellationToken);
        sessions.SelectSemanticSearchCommand.Execute(null);
        Assert.True(sessions.IsSemanticSearchMode);
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user selects word search")]
    public Task WhenUserSelectsWordSearch() => HeadlessApp.RunAsync(_ =>
    {
        Sessions.SelectWordSearchCommand.Execute(null);
        Assert.False(Sessions.IsSemanticSearchMode);
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [Then("the search results exclude the session titled {string}")]
    public void ThenSearchResultsExcludeSession(string title) =>
        Assert.DoesNotContain(Sessions.SearchResults, session => session.Title == title);

    [Then("semantic search ranks the session titled {string} first")]
    public void ThenSemanticSearchRanksSessionFirst(string title)
    {
        Assert.True(Sessions.IsSemanticSearchMode);
        Assert.Equal(title, Sessions.SearchResults.FirstOrDefault()?.Title);
    }
}

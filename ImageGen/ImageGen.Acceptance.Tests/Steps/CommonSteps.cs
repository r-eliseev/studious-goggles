using System.Globalization;
using System.Net;
using ImageGen.Acceptance.Tests.Support;
using ImageGen.Core.Domain.Keys;
using Reqnroll;

namespace ImageGen.Acceptance.Tests.Steps;

[Binding]
public sealed class CommonSteps(ScenarioContext scenario)
{
    private const string UnexpectedRequestKey = "acceptance.unexpected-request";

    private AppWorld World => scenario.Get<AppWorld>();

    [BeforeScenario]
    public void CreateWorld() => scenario.Set(new AppWorld());

    [AfterScenario]
    public async Task DisposeWorldAsync()
    {
        var world = World;
        await HeadlessApp.RunAsync(async _ =>
        {
            await world.DisposeAsync();
            return true;
        }, CancellationToken.None).ConfigureAwait(false);
    }

    [Given("the app is launched for the first time")]
    public async Task GivenTheAppIsLaunchedForTheFirstTime()
    {
        await HeadlessApp.RunAsync(async cancellationToken =>
        {
            var savedKey = await World.Secrets.GetAsync(SecretNames.OpenRouterApiKey, cancellationToken);
            Assert.Null(savedKey);
            await World.StartAsync(cancellationToken);
            return true;
        }, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    [Given("the user has a saved OpenRouter key")]
    public async Task GivenTheUserHasASavedOpenRouterKey()
    {
        await World.Secrets.SetAsync(SecretNames.OpenRouterApiKey, "e2e-mock-key", TestContext.Current.CancellationToken).ConfigureAwait(false);
        World.Network.RegisterSecret("e2e-mock-key");
        await HeadlessApp.RunAsync(async cancellationToken =>
        {
            await World.StartAsync(cancellationToken);
            return true;
        }, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    [Given("OpenRouter rejects key checks with HTTP {int}")]
    public void GivenOpenRouterRejectsKeyChecksWithHttp(int statusCode) => World.OpenRouter.SetKeyResponse((HttpStatusCode)statusCode);

    [Given("OpenRouter has no connection")]
    public void GivenOpenRouterHasNoConnection() => World.OpenRouter.SetKeyNetworkError();

    [When("the current time advances by {int} day")]
    public void WhenCurrentTimeAdvancesByDays(int days) => World.Time.Advance(TimeSpan.FromDays(days));

    [Given("the OpenRouter image model catalog:")]
    public void GivenTheOpenRouterImageModelCatalog(DataTable table)
    {
        var models = table.Rows.Select(row => new FakeOpenRouter.ImageModel(
            row["id"],
            row["name"],
            bool.Parse(row["seed"]),
            ParseNullableDecimal(row["price per MP"])));
        World.OpenRouter.SetImageModels(models);
    }

    [Given("the provider answers generation for model {string} with:")]
    public void GivenTheProviderAnswersGenerationForModelWith(string modelId, DataTable table)
    {
        var row = Assert.Single(table.Rows);
        switch (row["response"].ToLowerInvariant())
        {
            case "picture":
                World.OpenRouter.SetImage(
                    modelId,
                    ParseInteger(row["width"], 64),
                    ParseInteger(row["height"], 64),
                    ParseNullableDecimal(row["cost"]) ?? 0.01m);
                break;
            case "filtering":
                World.OpenRouter.SetFiltered(modelId);
                break;
            case "error":
                World.OpenRouter.SetGenerationError(modelId, (HttpStatusCode)ParseInteger(row["status"], 500), row["body"]);
                break;
            case "hold":
                _ = World.OpenRouter.HoldGeneration(modelId);
                break;
            default:
                throw new ArgumentException($"Unknown generation response '{row["response"]}'.", nameof(table));
        }
    }

    [Given("the assistant answers with:")]
    public void GivenTheAssistantAnswersWith(DataTable table)
    {
        var row = Assert.Single(table.Rows);
        switch (row["response"].ToLowerInvariant())
        {
            case "text":
                World.OpenRouter.QueueAssistantText(row["text"]);
                break;
            case "generate_images":
                World.OpenRouter.QueueGenerateImages(row["prompt"]);
                break;
            case "generate_video":
                World.OpenRouter.QueueGenerateVideo(row["prompt"]);
                break;
            case "error":
                World.OpenRouter.QueueAssistantError((HttpStatusCode)ParseInteger(row["status"], 500), row["body"]);
                break;
            default:
                throw new ArgumentException($"Unknown assistant response '{row["response"]}'.", nameof(table));
        }
    }

    [When("the user restarts the app")]
    public Task WhenTheUserRestartsTheApp() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.RestartAsync(cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the app sent no requests except:")]
    public void ThenTheAppSentNoRequestsExcept(DataTable table)
    {
        var expected = table.Rows.Select(row => $"{row["method"].ToUpperInvariant()} {row["address"]}").Order(StringComparer.Ordinal).ToArray();
        var actual = World.Network.Requests.Select(request => $"{request.Method} {request.Address}").Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, actual);
        foreach (var request in World.Network.Requests)
        {
            Assert.DoesNotContain("Authorization", request.Address, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("e2e-mock-key", request.Body ?? string.Empty, StringComparison.Ordinal);
        }
    }

    [When("a request is sent to an unconfigured fake route")]
    public async Task WhenARequestIsSentToAnUnconfiguredFakeRoute()
    {
        using var client = new HttpClient(World.Network, disposeHandler: false);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetAsync("https://unexpected.example.test/unconfigured", TestContext.Current.CancellationToken)).ConfigureAwait(false);
        scenario.Set(exception, UnexpectedRequestKey);
    }

    [Then("the fake network rejects it as unexpected")]
    public void ThenTheFakeNetworkRejectsItAsUnexpected()
    {
        var exception = scenario.Get<InvalidOperationException>(UnexpectedRequestKey);
        Assert.Contains("Unexpected request GET https://unexpected.example.test/unconfigured", exception.Message, StringComparison.Ordinal);
    }

    private static decimal? ParseNullableDecimal(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Equals("unknown", StringComparison.OrdinalIgnoreCase)
            ? null
            : decimal.Parse(value, CultureInfo.InvariantCulture);

    private static int ParseInteger(string value, int fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : int.Parse(value, CultureInfo.InvariantCulture);
}

using System.Globalization;
using System.Net;
using System.Text.Json;
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

    [Given("the app has a configured chat provider {string}")]
    public async Task GivenTheAppHasAConfiguredChatProvider(string providerId)
    {
        Assert.Equal("e2e-mock", providerId);
        await World.ConfigureChatProviderAsync(TestContext.Current.CancellationToken);
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

    [Given("the user accepts the image consent")]
    public void GivenTheUserAcceptsTheImageConsent() => World.Platform.QueueConfirmation(true);

    [Given("the OpenRouter image model catalog:")]
    public void GivenTheOpenRouterImageModelCatalog(DataTable table) => World.OpenRouter.SetImageModels(ParseImageModels(table));

    [When("the OpenRouter image model catalog changes to:")]
    public void WhenTheOpenRouterImageModelCatalogChangesTo(DataTable table) => World.OpenRouter.SetImageModels(ParseImageModels(table));

    [Given("the OpenRouter assistant model catalog:")]
    public void GivenTheOpenRouterAssistantModelCatalog(DataTable table) => World.OpenRouter.SetAssistantModels(
        table.Rows.Select(row => new FakeOpenRouter.AssistantModel(row["id"], row["name"])).ToArray());

    private static IReadOnlyList<FakeOpenRouter.ImageModel> ParseImageModels(DataTable table) => table.Rows.Select(row => new FakeOpenRouter.ImageModel(
            row["id"],
            row["name"],
            bool.Parse(row["seed"]),
            ParseNullableDecimal(row["price per MP"]),
            table.Header.Contains("price per image", StringComparer.OrdinalIgnoreCase)
                ? ParseNullableDecimal(row["price per image"])
                : null,
            ParseList(table, row, "aspect ratios"),
            ParseList(table, row, "resolutions"),
            ParseList(table, row, "formats"),
            ParseList(table, row, "qualities"),
            table.Header.Contains("input references", StringComparer.OrdinalIgnoreCase)
                ? ParseNullableInt(row["input references"])
                : null)).ToArray();

    [Then("the {string} request to OpenRouter has a value at JSON path {string}")]
    public async Task ThenTheRequestToOpenRouterHasAValueAtJsonPath(string requestKind, string jsonPath)
    {
        var requestPath = requestKind switch
        {
            "image generation" => "/api/v1/images",
            "chat completion" => "/api/v1/chat/completions",
            _ => throw new ArgumentException($"Unknown OpenRouter request kind '{requestKind}'.", nameof(requestKind)),
        };
        await World.WaitUntilAsync(
            () => World.Network.Requests.Any(item => item.Method == "POST" && new Uri(item.Address).AbsolutePath == requestPath),
            $"the OpenRouter {requestKind} request to be recorded",
            TestContext.Current.CancellationToken);
        var request = Assert.Single(World.Network.Requests, item =>
            item.Method == "POST" && new Uri(item.Address).AbsolutePath == requestPath);
        using var document = JsonDocument.Parse(request.Body!);
        Assert.True(TryGetJsonPath(document.RootElement, jsonPath, out var value), $"JSON path '{jsonPath}' was not found.");
        Assert.NotEqual(JsonValueKind.Null, value.ValueKind);
        if (scenario.TryGetValue("chat.reference-image-data-url", out string expectedReference))
        {
            Assert.Equal(expectedReference, value.GetString());
        }
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

    private static int? ParseNullableInt(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : int.Parse(value, CultureInfo.InvariantCulture);

    private static int ParseInteger(string value, int fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : int.Parse(value, CultureInfo.InvariantCulture);

    private static IReadOnlyList<string>? ParseList(DataTable table, DataTableRow row, string column) =>
        table.Header.Contains(column, StringComparer.OrdinalIgnoreCase)
            ? row[column].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : null;

    private static bool TryGetJsonPath(JsonElement root, string path, out JsonElement value)
    {
        value = root;
        var position = path.StartsWith('$') ? 1 : 0;
        while (position < path.Length)
        {
            if (path[position] == '.')
            {
                var start = ++position;
                while (position < path.Length && path[position] is not '.' and not '[') position++;
                if (position == start || value.ValueKind != JsonValueKind.Object
                    || !value.TryGetProperty(path[start..position], out var propertyValue))
                {
                    return false;
                }

                value = propertyValue;
            }
            else if (path[position] == '[')
            {
                var end = path.IndexOf(']', position + 1);
                if (end < 0 || !int.TryParse(path.AsSpan(position + 1, end - position - 1), out var index)
                    || value.ValueKind != JsonValueKind.Array || index < 0 || index >= value.GetArrayLength())
                {
                    return false;
                }

                value = value[index];
                position = end + 1;
            }
            else
            {
                return false;
            }
        }

        return true;
    }
}

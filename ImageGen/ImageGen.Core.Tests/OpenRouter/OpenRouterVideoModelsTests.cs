using System.Net;
using System.Text;
using System.Text.Json;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.OpenRouter;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Tests.TestDoubles;
using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Tests.OpenRouter;

public sealed class OpenRouterVideoModelsTests
{
    private const string ApiKey = "sk-or-test-1234";
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListVideoModelsAsync_CatalogFixture_MapsFieldsAndFiltersModelsWithoutDurations()
    {
        var json = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "OpenRouter", "Fixtures", "video-models.json"),
            TestContext.Current.CancellationToken);
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var models = await client.ListVideoModelsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            ["google/veo-3.1", "kwaivgi/kling-v3.0-std", "runway/gen-4.5", "bytedance/seedance-2.0"],
            models.Select(model => model.Id));
        var veo = models[0];
        Assert.Equal(ProviderIds.OpenRouter, veo.ProviderId);
        Assert.Equal("Google: Veo 3.1", veo.Name);
        Assert.Equal([4, 6, 8], veo.Durations);
        Assert.Equal(["720p", "1080p", "4K"], veo.Resolutions);
        Assert.Equal(["16:9", "9:16"], veo.AspectRatios);
        Assert.Equal(["first_frame", "last_frame"], veo.FrameImages);
        Assert.True(veo.GeneratesAudio);
        Assert.True(veo.SupportsSeed);
        Assert.Equal(0.40m, veo.PriceSkus["duration_seconds_with_audio"]);
        Assert.Equal(0.60m, veo.PriceSkus["duration_seconds_with_audio_4k"]);
        Assert.Equal(Now, veo.FetchedAt);
        Assert.Equal("https://openrouter.ai/api/v1/videos/models", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal("Bearer " + ApiKey, handler.Requests[0].Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task ListVideoModelsAsync_UnparseablePrice_LogsDebugAndKeepsOtherPrices()
    {
        const string json = """
            {"data":[{"id":"vendor/video","name":"Video","supported_durations":[5],
              "pricing_skus":{"duration_seconds":"not-a-number","duration_seconds_with_audio":"0.12"}}]}
            """;
        var logger = new CapturingLogger<OpenRouterClient>();
        var client = CreateClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, json), logger);

        var model = Assert.Single(await client.ListVideoModelsAsync(TestContext.Current.CancellationToken));

        Assert.Single(model.PriceSkus);
        Assert.Equal(0.12m, model.PriceSkus["duration_seconds_with_audio"]);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Debug && entry.Message.Contains("duration_seconds", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListVideoModelsAsync_MissingKey_ThrowsInvalidKeyWithoutRequest()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
        var client = CreateClient(handler, storedKey: null);

        var error = await Assert.ThrowsAsync<ProviderException>(() => client.ListVideoModelsAsync(TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.InvalidKey, error.Kind);
        Assert.Empty(handler.Requests);
    }

    private static OpenRouterClient CreateClient(
        StubHttpMessageHandler handler,
        ILogger<OpenRouterClient>? logger = null,
        string? storedKey = ApiKey)
    {
        var secrets = Substitute.For<ISecretStore>();
        secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns(storedKey);
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(ProviderIds.OpenRouter).Returns(new HttpClient(handler));
        var config = ProviderConfigLoader.Build(ProviderConfigLoader.ReadBuiltInJson(), null).Get(ProviderIds.OpenRouter);
        return new OpenRouterClient(config, factory, secrets, time, logger);
    }
}

using System.Net;
using System.Text;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Fal;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Tests.TestDoubles;

namespace ImageGen.Core.Tests.Fal;

public class FalImageProviderTests
{
    private const string Key = "fal-test-key";
    private const string Endpoint = "fal-ai/flux/dev";
    private static readonly Uri FalAddress = new("https://queue.fal.run/");
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private static readonly string SubmitJson = """
        {"request_id":"req-1","status_url":"https://queue.fal.run/fal-ai/flux/dev/requests/req-1/status",
         "response_url":"https://queue.fal.run/fal-ai/flux/dev/requests/req-1",
         "cancel_url":"https://queue.fal.run/fal-ai/flux/dev/requests/req-1/cancel"}
        """;

    private static readonly ProviderJobHandle Handle = new(
        ProviderIds.Fal,
        "req-1",
        "https://queue.fal.run/fal-ai/flux/dev/requests/req-1/status",
        "https://queue.fal.run/fal-ai/flux/dev/requests/req-1",
        "https://queue.fal.run/fal-ai/flux/dev/requests/req-1/cancel");

    private static FalImageProvider CreateProvider(StubHttpMessageHandler handler, string? storedKey = Key)
    {
        var secrets = Substitute.For<ISecretStore>();
        secrets.GetAsync(SecretNames.ForProvider(ProviderIds.Fal), Arg.Any<CancellationToken>()).Returns(storedKey);
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(ProviderIds.Fal).Returns(new HttpClient(handler));
        var config = new ProviderConfig(ProviderIds.Fal, ProviderKind.Fal, "fal.ai", FalAddress, TimeSpan.FromSeconds(60), []);
        return new FalImageProvider(config, factory, secrets, time);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static ImageGenerationRequest Request(long? seed = null) =>
        new(Endpoint, "a red fox", seed, null, null, null, null, []);

    [Fact]
    public async Task Submit_PostsPromptToEndpointWithKey()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, SubmitJson));
        var provider = CreateProvider(handler);

        await provider.SubmitAsync(Request(seed: 7), TestContext.Current.CancellationToken);

        var sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("https://queue.fal.run/fal-ai/flux/dev", sent.RequestUri!.ToString());
        Assert.Equal("Key " + Key, sent.Headers.GetValues("Authorization").Single());
        Assert.Contains("\"prompt\":\"a red fox\"", handler.RequestBodies.Single());
        Assert.Contains("\"seed\":7", handler.RequestBodies.Single());
    }

    [Fact]
    public async Task Submit_WithoutSeed_SendsNoSeedField()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, SubmitJson));
        var provider = CreateProvider(handler);

        await provider.SubmitAsync(Request(seed: null), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("seed", handler.RequestBodies.Single());
    }

    [Fact]
    public async Task Submit_KeepsAddressesFromResponse()
    {
        var provider = CreateProvider(new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, SubmitJson)));

        var handle = await provider.SubmitAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(Handle, handle);
    }

    [Fact]
    public async Task Submit_WithoutKey_ThrowsInvalidKeyWithoutRequest()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, SubmitJson));
        var provider = CreateProvider(handler, storedKey: null);

        var ex = await Assert.ThrowsAsync<ProviderException>(() => provider.SubmitAsync(Request(), TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.InvalidKey, ex.Kind);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Poll_InQueue_ReportsPosition()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, """{"status":"IN_QUEUE","queue_position":3}"""));
        var provider = CreateProvider(handler);

        var update = await provider.PollAsync(Handle, TestContext.Current.CancellationToken);

        Assert.Equal(JobState.Queued, update.State);
        Assert.Equal(3, update.QueuePosition);
        Assert.Equal(Handle.StatusUrl + "?logs=1", handler.Requests.Single().RequestUri!.ToString());
    }

    [Fact]
    public async Task Poll_InProgress_ReportsLogs()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK,
            """{"status":"IN_PROGRESS","logs":[{"message":"step 1"},{"message":"step 2"}]}"""));
        var provider = CreateProvider(handler);

        var update = await provider.PollAsync(Handle, TestContext.Current.CancellationToken);

        Assert.Equal(JobState.Running, update.State);
        Assert.Equal("step 1\nstep 2", update.Log);
    }

    [Fact]
    public async Task Poll_CompletedWithError_Fails()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK,
            """{"status":"COMPLETED","error":"bad input","error_type":"validation_error"}"""));
        var provider = CreateProvider(handler);

        var update = await provider.PollAsync(Handle, TestContext.Current.CancellationToken);

        Assert.Equal(JobState.Failed, update.State);
        Assert.Equal(ErrorKind.BadRequest, update.ErrorKind);
        Assert.Contains("bad input", update.ErrorMessage);
    }

    [Fact]
    public async Task Poll_CompletedWithNsfwFlag_IsFilteredAndNothingIsDownloaded()
    {
        var handler = new StubHttpMessageHandler(req => req.RequestUri!.ToString().EndsWith("/requests/req-1", StringComparison.Ordinal)
            ? Json(HttpStatusCode.OK, """{"images":[{"url":"https://cdn/img.png","content_type":"image/png"}],"has_nsfw_concepts":[true]}""")
            : Json(HttpStatusCode.OK, """{"status":"COMPLETED"}"""));
        var provider = CreateProvider(handler);

        var update = await provider.PollAsync(Handle, TestContext.Current.CancellationToken);

        Assert.Equal(JobState.Filtered, update.State);
        Assert.Equal(ErrorKind.Moderated, update.ErrorKind);
        Assert.DoesNotContain(handler.Requests, r => r.RequestUri!.Host == "cdn");
    }

    [Fact]
    public async Task Poll_Completed_DownloadsImage()
    {
        var handler = new StubHttpMessageHandler(req =>
        {
            if (req.RequestUri!.Host == "cdn")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([9, 8, 7]) };
            }

            return req.RequestUri.ToString().EndsWith("/requests/req-1", StringComparison.Ordinal)
                ? Json(HttpStatusCode.OK, """{"images":[{"url":"https://cdn/img.png","content_type":"image/png"}],"has_nsfw_concepts":[false]}""")
                : Json(HttpStatusCode.OK, """{"status":"COMPLETED"}""");
        });
        var provider = CreateProvider(handler);

        var update = await provider.PollAsync(Handle, TestContext.Current.CancellationToken);

        Assert.Equal(JobState.Succeeded, update.State);
        Assert.Equal(new byte[] { 9, 8, 7 }, update.Result!.Content);
        Assert.Equal("image/png", update.Result.MediaType);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "", CancelOutcome.Cancelled)]
    [InlineData(HttpStatusCode.Accepted, "", CancelOutcome.CancellationRequested)]
    [InlineData(HttpStatusCode.BadRequest, """{"status":"ALREADY_COMPLETED"}""", CancelOutcome.AlreadyCompleted)]
    [InlineData(HttpStatusCode.NotFound, """{"status":"NOT_FOUND"}""", CancelOutcome.NotFound)]
    public async Task Cancel_MapsStatusCodeToOutcome(HttpStatusCode status, string body, CancelOutcome expected)
    {
        var handler = new StubHttpMessageHandler(_ => Json(status, body));
        var provider = CreateProvider(handler);

        var outcome = await provider.CancelAsync(Handle, TestContext.Current.CancellationToken);

        Assert.Equal(expected, outcome);
        Assert.Equal(HttpMethod.Put, handler.Requests.Single().Method);
        Assert.Equal(Handle.CancelUrl, handler.Requests.Single().RequestUri!.ToString());
    }

    [Fact]
    public async Task ListImageModels_FollowsCursorAndMapsEndpoints()
    {
        var handler = new StubHttpMessageHandler(req => req.RequestUri!.Query.Contains("cursor=c2", StringComparison.Ordinal)
            ? Json(HttpStatusCode.OK, """{"models":[{"endpoint_id":"fal-ai/b","metadata":{"display_name":"B"}}],"has_more":false,"next_cursor":null}""")
            : Json(HttpStatusCode.OK, """{"models":[{"endpoint_id":"fal-ai/a","metadata":{"display_name":"A"}}],"has_more":true,"next_cursor":"c2"}"""));
        var provider = CreateProvider(handler);

        var models = await provider.ListImageModelsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["fal-ai/a", "fal-ai/b"], models.Select(m => m.Id));
        Assert.Equal(["A", "B"], models.Select(m => m.Name));
        Assert.All(models, m => Assert.Equal(ProviderIds.Fal, m.ProviderId));
        Assert.Equal(2, handler.Requests.Count);
        Assert.StartsWith("https://api.fal.ai/v1/models", handler.Requests[0].RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Contains("category=text-to-image", handler.Requests[0].RequestUri!.Query, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, ApiKeyStatus.Valid)]
    [InlineData(HttpStatusCode.Unauthorized, ApiKeyStatus.Invalid)]
    public async Task CheckKey_MapsStatusToKeyStatus(HttpStatusCode status, ApiKeyStatus expected)
    {
        var handler = new StubHttpMessageHandler(_ => Json(status, "{}"));
        var provider = CreateProvider(handler);

        var result = await provider.CheckKeyAsync(Key, TestContext.Current.CancellationToken);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GenerateAsync_SubmitsAndWaitsForTheImage()
    {
        var handler = new StubHttpMessageHandler(req =>
        {
            var url = req.RequestUri!.ToString();
            if (req.RequestUri.Host == "cdn")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([5, 6]) };
            }

            if (req.Method == HttpMethod.Post)
            {
                return Json(HttpStatusCode.OK, SubmitJson);
            }

            if (url.Contains("/status", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, """{"status":"COMPLETED"}""");
            }

            return Json(HttpStatusCode.OK, """{"images":[{"url":"https://cdn/x.png","content_type":"image/png"}],"has_nsfw_concepts":[false]}""");
        });
        var provider = CreateProvider(handler);

        var result = await provider.GenerateAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(new byte[] { 5, 6 }, result.Content);
        Assert.Equal("image/png", result.MediaType);
    }

    [Fact]
    public async Task GenerateAsync_Filtered_ThrowsModerated()
    {
        var handler = new StubHttpMessageHandler(req =>
        {
            if (req.Method == HttpMethod.Post)
            {
                return Json(HttpStatusCode.OK, SubmitJson);
            }

            return req.RequestUri!.ToString().Contains("/status", StringComparison.Ordinal)
                ? Json(HttpStatusCode.OK, """{"status":"COMPLETED"}""")
                : Json(HttpStatusCode.OK, """{"images":[{"url":"https://cdn/x.png"}],"has_nsfw_concepts":[true]}""");
        });
        var provider = CreateProvider(handler);

        var ex = await Assert.ThrowsAsync<ProviderException>(() => provider.GenerateAsync(Request(), TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.Moderated, ex.Kind);
    }
}

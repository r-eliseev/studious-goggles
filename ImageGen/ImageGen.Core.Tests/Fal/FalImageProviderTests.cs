using System.Net;
using System.Net.Http.Headers;
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

    private static FalImageProvider CreateProvider(StubHttpMessageHandler handler, string? storedKey = Key, TimeSpan? timeout = null)
    {
        var secrets = Substitute.For<ISecretStore>();
        secrets.GetAsync(SecretNames.ForProvider(ProviderIds.Fal), Arg.Any<CancellationToken>()).Returns(storedKey);
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(ProviderIds.Fal).Returns(new HttpClient(handler));
        var config = new ProviderConfig(ProviderIds.Fal, ProviderKind.Fal, "fal.ai", FalAddress, timeout ?? TimeSpan.FromSeconds(60), []);
        return new FalImageProvider(config, factory, secrets, time);
    }

    private static StubHttpMessageHandler HangingHandler(TaskCompletionSource entered) => new(async (_, token) =>
    {
        entered.TrySetResult();
        await Task.Delay(Timeout.Infinite, token);
        return Json(HttpStatusCode.OK, "{}");
    });

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static ImageGenerationRequest Request(long? seed = null) =>
        new(Endpoint, "a red fox", seed, null, null, null, null, []);

    private sealed class BlockingContent(TaskCompletionSource entered) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.Delay(Timeout.Infinite);

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

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
    public async Task Submit_ConfigTimeout_ThrowsTimeout()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = CreateProvider(HangingHandler(entered), timeout: TimeSpan.FromMilliseconds(40));

        var operation = provider.SubmitAsync(Request(), TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<ProviderException>(() => operation);

        Assert.Equal(ErrorKind.Timeout, error.Kind);
    }

    [Fact]
    public async Task CheckKey_ConfigTimeout_ReturnsNetworkError()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = CreateProvider(HangingHandler(entered), timeout: TimeSpan.FromMilliseconds(40));

        var operation = provider.CheckKeyAsync(Key, TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(ApiKeyStatus.NetworkError, await operation);
    }

    [Fact]
    public async Task ListImageModels_ConfigTimeout_ThrowsTimeout()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = CreateProvider(HangingHandler(entered), timeout: TimeSpan.FromMilliseconds(40));

        var operation = provider.ListImageModelsAsync(TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<ProviderException>(() => operation);

        Assert.Equal(ErrorKind.Timeout, error.Kind);
    }

    [Fact]
    public async Task Poll_StatusConfigTimeout_ThrowsTimeout()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = CreateProvider(HangingHandler(entered), timeout: TimeSpan.FromMilliseconds(40));

        var operation = provider.PollAsync(Handle, TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<ProviderException>(() => operation);

        Assert.Equal(ErrorKind.Timeout, error.Kind);
    }

    [Fact]
    public async Task Poll_ResultConfigTimeout_ThrowsTimeout()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler(async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/status", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, "{\"status\":\"COMPLETED\"}");
            }

            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return Json(HttpStatusCode.OK, "{}");
        });
        var provider = CreateProvider(handler, timeout: TimeSpan.FromMilliseconds(40));

        var operation = provider.PollAsync(Handle, TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<ProviderException>(() => operation);

        Assert.Equal(ErrorKind.Timeout, error.Kind);
    }

    [Theory]
    [InlineData("download")]
    [InlineData("cancel")]
    public async Task DirectRequest_ConfigTimeout_ThrowsTimeout(string phase)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler(async (request, token) =>
        {
            var matches = phase == "cancel"
                ? request.Method == HttpMethod.Put
                : request.RequestUri!.Host == "cdn";
            if (matches)
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
            }

            if (phase == "download" && request.RequestUri!.AbsolutePath.EndsWith("/status", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, "{\"status\":\"COMPLETED\"}");
            }

            if (phase == "download" && request.RequestUri!.Host != "cdn")
            {
                return Json(HttpStatusCode.OK, "{\"images\":[{\"url\":\"https://cdn/x.png\"}],\"has_nsfw_concepts\":[false]}");
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var provider = CreateProvider(handler, timeout: TimeSpan.FromMilliseconds(40));
        Task operation = phase == "cancel"
            ? provider.CancelAsync(Handle, TestContext.Current.CancellationToken)
            : provider.PollAsync(Handle, TestContext.Current.CancellationToken);

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<ProviderException>(() => operation);

        Assert.Equal(ErrorKind.Timeout, error.Kind);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("download")]
    [InlineData("cancel")]
    public async Task DirectRequest_CallerCancellation_RemainsOperationCanceled(string phase)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler(async (request, token) =>
        {
            var matches = phase switch
            {
                "cancel" => request.Method == HttpMethod.Put,
                "download" => request.RequestUri!.Host == "cdn",
                _ => request.RequestUri!.AbsolutePath.EndsWith("/status", StringComparison.Ordinal),
            };
            if (matches)
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
            }

            if (phase != "cancel" && request.RequestUri!.AbsolutePath.EndsWith("/status", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, "{\"status\":\"COMPLETED\"}");
            }

            if (phase != "cancel" && request.RequestUri!.Host != "cdn")
            {
                return Json(HttpStatusCode.OK, "{\"images\":[{\"url\":\"https://cdn/x.png\"}],\"has_nsfw_concepts\":[false]}");
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var provider = CreateProvider(handler, timeout: TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        Task operation = phase switch
        {
            "cancel" => provider.CancelAsync(Handle, cancellation.Token),
            "download" or "json" => provider.PollAsync(Handle, cancellation.Token),
            _ => throw new ArgumentOutOfRangeException(nameof(phase)),
        };

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
    }

    [Fact]
    public async Task JsonBodyRead_ConfigTimeout_ThrowsTimeout()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new BlockingContent(entered),
        });
        var provider = CreateProvider(handler, timeout: TimeSpan.FromMilliseconds(40));

        var operation = provider.ListImageModelsAsync(TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<ProviderException>(() => operation);

        Assert.Equal(ErrorKind.Timeout, error.Kind);
    }

    [Fact]
    public async Task JsonBodyRead_CallerCancellation_RemainsOperationCanceled()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new BlockingContent(entered),
        });
        var provider = CreateProvider(handler, timeout: TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();

        var operation = provider.ListImageModelsAsync(cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
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

    [Theory]
    [InlineData("https://evil.example/model")]
    [InlineData("//evil.example/model")]
    [InlineData("fal-ai/../secret")]
    public async Task Submit_InvalidModelEndpoint_IsRejectedBeforeReadingKeyOrSending(string modelId)
    {
        var secrets = Substitute.For<ISecretStore>();
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, SubmitJson));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(ProviderIds.Fal).Returns(new HttpClient(handler));
        var provider = new FalImageProvider(
            new ProviderConfig(ProviderIds.Fal, ProviderKind.Fal, "fal.ai", FalAddress, TimeSpan.FromSeconds(60), []),
            factory,
            secrets,
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<ProviderException>(() => provider.SubmitAsync(Request() with { ModelId = modelId }, TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.Unknown, exception.Kind);
        Assert.DoesNotContain(modelId, exception.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
        await secrets.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("status_url", "https://evil.example/status")]
    [InlineData("response_url", "https://evil.example/result")]
    [InlineData("cancel_url", "https://evil.example/cancel")]
    [InlineData("status_url", "")]
    public async Task Submit_RejectsCallbackAddressOutsideConfiguredOrigin(string field, string value)
    {
        var response = field switch
        {
            "status_url" => $$"""{"request_id":"req-1","status_url":"{{value}}","response_url":"https://queue.fal.run/result","cancel_url":"https://queue.fal.run/cancel"}""",
            "response_url" => $$"""{"request_id":"req-1","status_url":"https://queue.fal.run/status","response_url":"{{value}}","cancel_url":"https://queue.fal.run/cancel"}""",
            "cancel_url" => $$"""{"request_id":"req-1","status_url":"https://queue.fal.run/status","response_url":"https://queue.fal.run/result","cancel_url":"{{value}}"}""",
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, response));
        var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<ProviderException>(() => provider.SubmitAsync(Request(), TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.Unknown, exception.Kind);
        if (value.Length > 0)
        {
            Assert.DoesNotContain(value, exception.Message, StringComparison.Ordinal);
        }
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("poll")]
    [InlineData("cancel")]
    public async Task HandleWithDifferentProvider_IsRejectedBeforeReadingKeyOrSending(string operation)
    {
        var secrets = Substitute.For<ISecretStore>();
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, "{}"));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(ProviderIds.Fal).Returns(new HttpClient(handler));
        var provider = new FalImageProvider(
            new ProviderConfig(ProviderIds.Fal, ProviderKind.Fal, "fal.ai", FalAddress, TimeSpan.FromSeconds(60), []),
            factory,
            secrets,
            TimeProvider.System);
        var invalidHandle = Handle with { ProviderId = "other-provider" };

        var exception = await Assert.ThrowsAsync<ProviderException>(() => operation == "poll"
            ? provider.PollAsync(invalidHandle, TestContext.Current.CancellationToken)
            : provider.CancelAsync(invalidHandle, TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.Unknown, exception.Kind);
        Assert.Empty(handler.Requests);
        await secrets.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
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
    public async Task Poll_AddsLogsParameterWithoutDiscardingExistingQuery()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, """{"status":"IN_QUEUE"}"""));
        var provider = CreateProvider(handler);
        var handle = Handle with { StatusUrl = Handle.StatusUrl + "?trace=1&logs=0" };

        await provider.PollAsync(handle, TestContext.Current.CancellationToken);

        var query = handler.Requests.Single().RequestUri!.Query;
        Assert.Contains("trace=1", query, StringComparison.Ordinal);
        Assert.Contains("logs=1", query, StringComparison.Ordinal);
        Assert.DoesNotContain("logs=0", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Poll_RejectsPersistedCrossOriginHandleBeforeReadingKeyOrSending()
    {
        var secrets = Substitute.For<ISecretStore>();
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, "{}"));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(ProviderIds.Fal).Returns(new HttpClient(handler));
        var provider = new FalImageProvider(
            new ProviderConfig(ProviderIds.Fal, ProviderKind.Fal, "fal.ai", FalAddress, TimeSpan.FromSeconds(60), []),
            factory,
            secrets,
            TimeProvider.System);
        var invalidHandle = Handle with { ResponseUrl = "https://evil.example/result" };

        var exception = await Assert.ThrowsAsync<ProviderException>(() => provider.PollAsync(invalidHandle, TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.Unknown, exception.Kind);
        Assert.DoesNotContain("evil.example", exception.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
        await secrets.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
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
    [InlineData("https://127.1/x.png")]
    [InlineData("https://[::ffff:127.0.0.1]/x.png")]
    [InlineData("https://localhost./x.png")]
    public async Task Poll_CompletedWithNonPublicImageUrl_IsRejectedWithoutDownload(string imageUrl)
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/status", StringComparison.Ordinal)
            ? Json(HttpStatusCode.OK, """{"status":"COMPLETED"}""")
            : Json(HttpStatusCode.OK, $$"""{"images":[{"url":"{{imageUrl}}"}],"has_nsfw_concepts":[false]}"""));
        var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<ProviderException>(() => provider.PollAsync(Handle, TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.Unknown, exception.Kind);
        Assert.DoesNotContain(imageUrl, exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.Equal("queue.fal.run", request.RequestUri!.Host));
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

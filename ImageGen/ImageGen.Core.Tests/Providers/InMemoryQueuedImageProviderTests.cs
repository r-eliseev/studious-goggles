using System.Collections.Concurrent;
using System.Diagnostics;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Providers;
using NSubstitute;

namespace ImageGen.Core.Tests.Providers;

public sealed class InMemoryQueuedImageProviderTests
{
    private static readonly ImageGenerationRequest Request = new("model-id", "a prompt", 7);
    private static readonly ImageGenerationResult Result = new([1, 2, 3], "image/png", 0.42m);

    [Fact]
    public async Task Submit_RegistersJobAndReturnsLocalHandle()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ImageGenerationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = Substitute.For<ISynchronousImageTransport>();
        transport.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            entered.TrySetResult();
            return release.Task;
        });
        var provider = NewProvider(transport);

        var handle = await provider.SubmitAsync(Request, TestContext.Current.CancellationToken);
        var initial = await provider.PollAsync(handle, TestContext.Current.CancellationToken);

        Assert.Equal("custom-provider", handle.ProviderId);
        Assert.Matches("^[0-9a-f]{32}$", handle.RequestId);
        Assert.Equal($"memory://custom-provider/{handle.RequestId}/status", handle.StatusUrl);
        Assert.Equal($"memory://custom-provider/{handle.RequestId}/result", handle.ResponseUrl);
        Assert.Equal($"memory://custom-provider/{handle.RequestId}/cancel", handle.CancelUrl);
        Assert.True(initial.State is JobState.Queued or JobState.Running);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        release.SetResult(Result);
        Assert.Equal(Result, (await WaitForTerminalAsync(provider, handle)).Result);
    }

    [Fact]
    public async Task Submit_CallerCancellationAfterReturn_DoesNotCancelTransport()
    {
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ImageGenerationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = Substitute.For<ISynchronousImageTransport>();
        transport.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            entered.TrySetResult(call.Arg<CancellationToken>());
            return release.Task;
        });
        var provider = NewProvider(transport);
        using var caller = new CancellationTokenSource();

        var handle = await provider.SubmitAsync(Request, caller.Token);
        var transportToken = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        caller.Cancel();
        Assert.False(transportToken.IsCancellationRequested);
        release.SetResult(Result);

        Assert.Equal(JobState.Succeeded, (await WaitForTerminalAsync(provider, handle)).State);
    }

    [Fact]
    public async Task Poll_CompletedJob_PreservesResultMediaTypeAndCost()
    {
        var (provider, handle, _, release) = await StartGatedAsync();
        release.SetResult(Result);

        var update = await WaitForTerminalAsync(provider, handle);

        Assert.Equal(JobState.Succeeded, update.State);
        Assert.Equal(Result, update.Result);
        Assert.Equal("image/png", update.Result!.MediaType);
        Assert.Equal(0.42m, update.Result.CostUsd);
    }

    [Theory]
    [InlineData(ErrorKind.Moderated, JobState.Filtered)]
    [InlineData(ErrorKind.Network, JobState.Failed)]
    [InlineData(ErrorKind.InvalidKey, JobState.Failed)]
    public async Task Poll_ProviderException_MapsStateAndPreservesError(ErrorKind kind, JobState expected)
    {
        var (provider, handle, _, release) = await StartGatedAsync();
        release.SetException(new ProviderException(kind, "provider message"));

        var update = await WaitForTerminalAsync(provider, handle);

        Assert.Equal(expected, update.State);
        Assert.Equal(kind, update.ErrorKind);
        Assert.Equal("provider message", update.ErrorMessage);
    }

    [Fact]
    public async Task Poll_UnexpectedException_MapsToFailedUnknown()
    {
        var (provider, handle, _, release) = await StartGatedAsync();
        release.SetException(new InvalidOperationException("unexpected"));

        var update = await WaitForTerminalAsync(provider, handle);

        Assert.Equal(JobState.Failed, update.State);
        Assert.Equal(ErrorKind.Unknown, update.ErrorKind);
        Assert.Equal("unexpected", update.ErrorMessage);
    }

    [Fact]
    public async Task Poll_UnrelatedOperationCanceled_MapsToFailedUnknown()
    {
        var (provider, handle, _, release) = await StartGatedAsync();
        release.SetCanceled(new CancellationToken(canceled: true));

        var update = await WaitForTerminalAsync(provider, handle);

        Assert.Equal(JobState.Failed, update.State);
        Assert.Equal(ErrorKind.Unknown, update.ErrorKind);
    }

    [Fact]
    public async Task Cancel_ActiveJob_IsIdempotentAndCancelsTransportToken()
    {
        var (provider, handle, transportToken, release) = await StartGatedAsync();

        var first = await provider.CancelAsync(handle, TestContext.Current.CancellationToken);
        var second = await provider.CancelAsync(handle, TestContext.Current.CancellationToken);

        Assert.Equal(CancelOutcome.CancellationRequested, first);
        Assert.Equal(first, second);
        Assert.True(transportToken.IsCancellationRequested);
        release.SetCanceled(transportToken);
        Assert.Equal(JobState.Cancelled, (await WaitForTerminalAsync(provider, handle)).State);
    }

    [Fact]
    public async Task Cancel_TransportIgnoresCancellation_LateSuccessIsCancelled()
    {
        var (provider, handle, _, release) = await StartGatedAsync();

        Assert.Equal(CancelOutcome.CancellationRequested,
            await provider.CancelAsync(handle, TestContext.Current.CancellationToken));
        release.SetResult(Result);

        var update = await WaitForTerminalAsync(provider, handle);
        Assert.Equal(JobState.Cancelled, update.State);
        Assert.Null(update.Result);
    }

    [Fact]
    public async Task Cancel_TerminalJob_AlreadyCompleted()
    {
        var (provider, handle, _, release) = await StartGatedAsync();
        release.SetResult(Result);
        await WaitForTerminalAsync(provider, handle);

        Assert.Equal(CancelOutcome.AlreadyCompleted,
            await provider.CancelAsync(handle, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cancel_UnknownJob_NotFound()
    {
        var provider = NewProvider(Substitute.For<ISynchronousImageTransport>());
        var handle = Handle("custom-provider", Guid.NewGuid().ToString("N"));

        Assert.Equal(CancelOutcome.NotFound,
            await provider.CancelAsync(handle, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Poll_UnknownJob_ReturnsInterruptedWithoutSubmitting()
    {
        var transport = Substitute.For<ISynchronousImageTransport>();
        var provider = NewProvider(transport);
        var handle = Handle("custom-provider", Guid.NewGuid().ToString("N"));

        var update = await provider.PollAsync(handle, TestContext.Current.CancellationToken);

        Assert.Equal(JobState.Failed, update.State);
        Assert.Equal(ErrorKind.Interrupted, update.ErrorKind);
        await transport.DidNotReceive().GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("other-provider", "valid")]
    [InlineData("custom-provider", "malformed")]
    public async Task Poll_InvalidHandle_ThrowsUnknownProviderException(string providerId, string requestId)
    {
        var provider = NewProvider(Substitute.For<ISynchronousImageTransport>());
        var handle = Handle(providerId, requestId);

        var error = await Assert.ThrowsAsync<ProviderException>(() =>
            provider.PollAsync(handle, TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.Unknown, error.Kind);
    }

    [Fact]
    public async Task Poll_TamperedLocalUrls_ThrowsWithoutTransportAccess()
    {
        var transport = Substitute.For<ISynchronousImageTransport>();
        var provider = NewProvider(transport);
        var valid = Handle("custom-provider", Guid.NewGuid().ToString("N"));
        var tampered = valid with { StatusUrl = "https://example.invalid/status" };

        await Assert.ThrowsAsync<ProviderException>(() => provider.PollAsync(tampered, TestContext.Current.CancellationToken));
        await transport.DidNotReceive().GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Poll_HandleFromPreviousProviderInstance_ReturnsInterrupted()
    {
        var transport = Substitute.For<ISynchronousImageTransport>();
        var first = NewProvider(transport);
        var (_, handle, _, release) = await StartGatedAsync(provider: first, transport: transport);
        release.SetResult(Result);
        await WaitForTerminalAsync(first, handle);
        var restarted = NewProvider(transport);

        var update = await restarted.PollAsync(handle, TestContext.Current.CancellationToken);

        Assert.Equal(JobState.Failed, update.State);
        Assert.Equal(ErrorKind.Interrupted, update.ErrorKind);
        await transport.Received(1).GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Poll_TerminalSnapshotExpiresAtExactTtlBoundary()
    {
        var time = new ManualTimeProvider();
        var (provider, handle, _, release) = await StartGatedAsync(timeProvider: time);
        release.SetResult(Result);
        await WaitForTerminalAsync(provider, handle);

        time.Advance(InMemoryQueuedImageProvider.TerminalRetention - TimeSpan.FromTicks(1));
        Assert.Equal(JobState.Succeeded, (await provider.PollAsync(handle, TestContext.Current.CancellationToken)).State);
        time.Advance(TimeSpan.FromTicks(1));

        var expired = await provider.PollAsync(handle, TestContext.Current.CancellationToken);
        Assert.Equal(JobState.Failed, expired.State);
        Assert.Equal(ErrorKind.Interrupted, expired.ErrorKind);
    }

    [Fact]
    public async Task Submit_SweepsExpiredTerminalJobs()
    {
        var time = new ManualTimeProvider();
        var transport = Substitute.For<ISynchronousImageTransport>();
        var gates = new ConcurrentQueue<TaskCompletionSource<ImageGenerationResult>>();
        transport.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            var gate = new TaskCompletionSource<ImageGenerationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            gates.Enqueue(gate);
            return gate.Task;
        });
        var provider = NewProvider(transport, time);
        var oldHandle = await provider.SubmitAsync(Request, TestContext.Current.CancellationToken);
        while (!gates.TryPeek(out _))
        {
            await Task.Yield();
        }
        gates.TryDequeue(out var oldGate);
        oldGate!.SetResult(Result);
        await WaitForTerminalAsync(provider, oldHandle);
        time.Advance(InMemoryQueuedImageProvider.TerminalRetention);

        var newHandle = await provider.SubmitAsync(Request, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.Interrupted,
            (await provider.PollAsync(oldHandle, TestContext.Current.CancellationToken)).ErrorKind);
        var nextGate = await WaitForGateAsync(gates);
        nextGate.SetResult(Result);
        await WaitForTerminalAsync(provider, newHandle);
    }

    [Fact]
    public async Task Cancel_SweepsExpiredTerminalJobs()
    {
        var time = new ManualTimeProvider();
        var (provider, handle, _, release) = await StartGatedAsync(timeProvider: time);
        release.SetResult(Result);
        await WaitForTerminalAsync(provider, handle);
        time.Advance(InMemoryQueuedImageProvider.TerminalRetention);

        Assert.Equal(CancelOutcome.NotFound,
            await provider.CancelAsync(handle, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Poll_AgeAdvance_DoesNotSweepActiveJob()
    {
        var time = new ManualTimeProvider();
        var (provider, handle, _, _) = await StartGatedAsync(timeProvider: time);
        time.Advance(InMemoryQueuedImageProvider.TerminalRetention + TimeSpan.FromMinutes(1));

        var update = await provider.PollAsync(handle, TestContext.Current.CancellationToken);

        Assert.Equal(JobState.Running, update.State);
    }

    [Fact]
    public async Task Submit_PreCancelledCaller_DoesNotRegisterOrStartTransport()
    {
        var transport = Substitute.For<ISynchronousImageTransport>();
        var provider = NewProvider(transport);
        using var caller = new CancellationTokenSource();
        caller.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.SubmitAsync(Request, caller.Token));
        await transport.DidNotReceive().GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_FiveJobs_StartWithoutAdapterConcurrencyLimit()
    {
        const int count = 5;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releases = new ConcurrentDictionary<CancellationToken, TaskCompletionSource<ImageGenerationResult>>();
        var transport = Substitute.For<ISynchronousImageTransport>();
        var started = 0;
        transport.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var release = new TaskCompletionSource<ImageGenerationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            releases[call.Arg<CancellationToken>()] = release;
            if (Interlocked.Increment(ref started) == count)
            {
                entered.TrySetResult();
            }
            return release.Task;
        });
        var provider = NewProvider(transport);

        var handles = await Task.WhenAll(Enumerable.Range(0, count)
            .Select(_ => provider.SubmitAsync(Request, TestContext.Current.CancellationToken)));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        foreach (var release in releases.Values)
        {
            release.SetResult(Result);
        }

        foreach (var handle in handles)
        {
            Assert.Equal(JobState.Succeeded, (await WaitForTerminalAsync(provider, handle)).State);
        }
    }

    private static InMemoryQueuedImageProvider NewProvider(
        ISynchronousImageTransport transport,
        TimeProvider? timeProvider = null) => new(transport, "custom-provider", timeProvider ?? new ManualTimeProvider());

    private static async Task<(InMemoryQueuedImageProvider Provider, ProviderJobHandle Handle, CancellationToken Token,
        TaskCompletionSource<ImageGenerationResult> Release)> StartGatedAsync(
        TimeProvider? timeProvider = null,
        ISynchronousImageTransport? transport = null,
        InMemoryQueuedImageProvider? provider = null)
    {
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ImageGenerationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        transport ??= Substitute.For<ISynchronousImageTransport>();
        transport.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            entered.TrySetResult(call.Arg<CancellationToken>());
            return release.Task;
        });
        provider ??= NewProvider(transport, timeProvider);
        var handle = await provider.SubmitAsync(Request, TestContext.Current.CancellationToken);
        var token = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        return (provider, handle, token, release);
    }

    private static async Task<JobUpdate> WaitForTerminalAsync(InMemoryQueuedImageProvider provider, ProviderJobHandle handle)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            var update = await provider.PollAsync(handle, TestContext.Current.CancellationToken);
            if (update.IsTerminal)
            {
                return update;
            }
            await Task.Yield();
        }
        throw new TimeoutException("The job did not reach a terminal state.");
    }

    private static async Task<TaskCompletionSource<ImageGenerationResult>> WaitForGateAsync(
        ConcurrentQueue<TaskCompletionSource<ImageGenerationResult>> gates)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            if (gates.TryDequeue(out var gate))
            {
                return gate;
            }
            await Task.Yield();
        }
        throw new TimeoutException("The transport did not start.");
    }

    private static ProviderJobHandle Handle(string providerId, string requestId) => new(
        providerId,
        requestId,
        $"memory://custom-provider/{requestId}/status",
        $"memory://custom-provider/{requestId}/result",
        $"memory://custom-provider/{requestId}/cancel");

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan amount) => _now += amount;
    }
}

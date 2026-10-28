using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Providers;
using NSubstitute;

namespace ImageGen.Core.Tests.Providers;

public class ProviderJobPollerTests
{
    private static readonly ProviderJobHandle Handle = new("fal", "req-1", "https://q/status", "https://q/response", "https://q/cancel");
    private static readonly TimeSpan Tiny = TimeSpan.FromMilliseconds(1);

    [Fact]
    public async Task WatchAsync_RepeatedStateIsNotReportedTwice()
    {
        var provider = Substitute.For<IQueuedImageProvider>();
        provider.PollAsync(Handle, Arg.Any<CancellationToken>()).Returns(
            new JobUpdate(JobState.Queued, QueuePosition: 3),
            new JobUpdate(JobState.Queued, QueuePosition: 3),
            new JobUpdate(JobState.Running),
            new JobUpdate(JobState.Succeeded, Result: new ImageGenerationResult([1], "image/png", null)));

        var updates = await Collect(provider);

        Assert.Equal([JobState.Queued, JobState.Running, JobState.Succeeded], updates.Select(u => u.State));
        Assert.Equal(3, updates[0].QueuePosition);
    }

    [Fact]
    public async Task WatchAsync_NetworkFailureIsRetriedWithoutResubmitting()
    {
        var provider = Substitute.For<IQueuedImageProvider>();
        provider.PollAsync(Handle, Arg.Any<CancellationToken>()).Returns<JobUpdate>(
            _ => throw new ProviderException(ErrorKind.Network, "down"),
            _ => throw new ProviderException(ErrorKind.Network, "down"),
            _ => new JobUpdate(JobState.Succeeded, Result: new ImageGenerationResult([1], "image/png", null)));

        var updates = await Collect(provider);

        Assert.Equal(JobState.Succeeded, Assert.Single(updates).State);
        await provider.DidNotReceive().SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WatchAsync_TooManyNetworkFailures_ReportsFailedNetwork()
    {
        var provider = Substitute.For<IQueuedImageProvider>();
        provider.PollAsync(Handle, Arg.Any<CancellationToken>())
            .Returns<JobUpdate>(_ => throw new ProviderException(ErrorKind.Network, "down"));

        var updates = await Collect(provider, maxNetworkFailures: 2);

        var last = updates[^1];
        Assert.Equal(JobState.Failed, last.State);
        Assert.Equal(ErrorKind.Network, last.ErrorKind);
    }

    [Fact]
    public async Task WatchAsync_StopsAfterTerminalState()
    {
        var provider = Substitute.For<IQueuedImageProvider>();
        provider.PollAsync(Handle, Arg.Any<CancellationToken>()).Returns(new JobUpdate(JobState.Cancelled));

        var updates = await Collect(provider);

        Assert.Single(updates);
        await provider.Received(1).PollAsync(Handle, Arg.Any<CancellationToken>());
    }

    private static async Task<List<JobUpdate>> Collect(IQueuedImageProvider provider, int maxNetworkFailures = 5)
    {
        var updates = new List<JobUpdate>();
        var ct = TestContext.Current.CancellationToken;
        await foreach (var update in ProviderJobPoller.WatchAsync(provider, Handle, Tiny, Tiny, maxNetworkFailures, ct))
        {
            updates.Add(update);
        }

        return updates;
    }
}

using ImageGen.Core.Domain.Generations;

namespace ImageGen.Core.Providers;

/// <summary>
/// Follows one job: reads its state with growing intervals (from <paramref name="minInterval"/> to
/// <paramref name="maxInterval"/>) and reports each change. A network error on a read is retried; the submit is never
/// repeated (it is a paid run). After <paramref name="maxNetworkFailures"/> errors in a row the watch reports
/// <see cref="JobState.Failed"/> with <see cref="ErrorKind.Network"/>. The watch ends at a final state.
/// </summary>
public static class ProviderJobPoller
{
    public static async IAsyncEnumerable<JobUpdate> WatchAsync(
        IQueuedImageProvider provider,
        ProviderJobHandle handle,
        TimeSpan minInterval,
        TimeSpan maxInterval,
        int maxNetworkFailures,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var interval = minInterval;
        var networkFailures = 0;
        JobUpdate? last = null;

        while (true)
        {
            JobUpdate update;
            string? networkError = null;
            try
            {
                update = await provider.PollAsync(handle, cancellationToken);
                networkFailures = 0;
            }
            catch (ProviderException ex) when (ex.Kind == ErrorKind.Network)
            {
                networkError = ex.Message;
                update = new JobUpdate(JobState.Queued);
            }

            if (networkError is not null)
            {
                networkFailures++;
                if (networkFailures >= maxNetworkFailures)
                {
                    yield return new JobUpdate(JobState.Failed, ErrorKind: ErrorKind.Network, ErrorMessage: networkError);
                    yield break;
                }

                await Task.Delay(interval, cancellationToken);
                interval = Grow(interval, maxInterval);
                continue;
            }

            if (last is null || !SameReading(last, update))
            {
                yield return update;
                last = update;
            }

            if (update.IsTerminal)
            {
                yield break;
            }

            await Task.Delay(interval, cancellationToken);
            interval = Grow(interval, maxInterval);
        }
    }

    private static TimeSpan Grow(TimeSpan current, TimeSpan max)
    {
        var doubled = current + current;
        return doubled > max ? max : doubled;
    }

    private static bool SameReading(JobUpdate a, JobUpdate b) =>
        a.State == b.State && a.QueuePosition == b.QueuePosition && a.Log == b.Log;
}

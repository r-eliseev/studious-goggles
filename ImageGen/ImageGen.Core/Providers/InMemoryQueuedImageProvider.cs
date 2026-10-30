using System.Collections.Concurrent;
using ImageGen.Core.Domain.Generations;

namespace ImageGen.Core.Providers;

/// <summary>Adapts a synchronous image transport to the in-memory queued provider contract.</summary>
public sealed class InMemoryQueuedImageProvider : IQueuedImageProvider
{
    public static readonly TimeSpan TerminalRetention = TimeSpan.FromMinutes(10);

    private readonly IImageGenerator _transport;
    private readonly string _providerId;
    private readonly string _encodedProviderId;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, JobEntry> _jobs = new(StringComparer.Ordinal);

    public InMemoryQueuedImageProvider(IImageGenerator transport, string providerId, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _transport = transport;
        _providerId = providerId;
        _encodedProviderId = Uri.EscapeDataString(providerId);
        _timeProvider = timeProvider;
    }

    public Task<ProviderJobHandle> SubmitAsync(ImageGenerationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        SweepExpired();

        var requestId = Guid.NewGuid().ToString("N");
        var entry = new JobEntry(CreateHandle(requestId));
        if (!_jobs.TryAdd(requestId, entry))
        {
            throw new InvalidOperationException("A duplicate in-memory request identifier was generated.");
        }

        // Do not pass the caller token: it only governs submit preparation, not the accepted transport job.
        entry.Work = Task.Run(() => RunWorkerAsync(entry, request));
        return Task.FromResult(entry.Handle);
    }

    public Task<JobUpdate> PollAsync(ProviderJobHandle handle, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var requestId = ValidateHandle(handle);
        SweepExpired();

        if (!_jobs.TryGetValue(requestId, out var entry))
        {
            return Task.FromResult(Interrupted());
        }

        lock (entry.Gate)
        {
            if (!IsCurrentEntry(requestId, entry))
            {
                return Task.FromResult(Interrupted());
            }

            return Task.FromResult(entry.Snapshot);
        }
    }

    public Task<CancelOutcome> CancelAsync(ProviderJobHandle handle, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var requestId = ValidateHandle(handle);
        SweepExpired();

        if (!_jobs.TryGetValue(requestId, out var entry))
        {
            return Task.FromResult(CancelOutcome.NotFound);
        }

        lock (entry.Gate)
        {
            if (!IsCurrentEntry(requestId, entry))
            {
                return Task.FromResult(CancelOutcome.NotFound);
            }

            if (entry.Snapshot.IsTerminal)
            {
                return Task.FromResult(CancelOutcome.AlreadyCompleted);
            }

            if (!entry.CancellationRequested)
            {
                entry.CancellationRequested = true;
                try
                {
                    entry.TransportCancellation.Cancel();
                }
                catch (AggregateException)
                {
                    // Cancellation callbacks are transport-owned. The job still observes the cancelled token.
                }
            }

            return Task.FromResult(CancelOutcome.CancellationRequested);
        }
    }

    private async Task RunWorkerAsync(JobEntry entry, ImageGenerationRequest request)
    {
        if (!SetRunning(entry))
        {
            return;
        }

        try
        {
            var result = await _transport.GenerateAsync(request, entry.TransportCancellation.Token).ConfigureAwait(false);
            Complete(entry, new JobUpdate(JobState.Succeeded, Result: result));
        }
        catch (ProviderException exception)
        {
            var state = exception.Kind == ErrorKind.Moderated ? JobState.Filtered : JobState.Failed;
            Complete(entry, new JobUpdate(state, ErrorKind: exception.Kind, ErrorMessage: exception.Message));
        }
        catch (OperationCanceledException exception)
        {
            if (IsCancellationRequested(entry))
            {
                Complete(entry, new JobUpdate(JobState.Cancelled));
            }
            else
            {
                Complete(entry, new JobUpdate(JobState.Failed, ErrorKind: ErrorKind.Unknown, ErrorMessage: exception.Message));
            }
        }
        catch (Exception exception)
        {
            Complete(entry, new JobUpdate(JobState.Failed, ErrorKind: ErrorKind.Unknown, ErrorMessage: exception.Message));
        }
    }

    private bool SetRunning(JobEntry entry)
    {
        lock (entry.Gate)
        {
            if (entry.CancellationRequested)
            {
                CompleteLocked(entry, new JobUpdate(JobState.Cancelled));
                return false;
            }

            entry.Snapshot = new JobUpdate(JobState.Running);
            return true;
        }
    }

    private void Complete(JobEntry entry, JobUpdate update)
    {
        lock (entry.Gate)
        {
            CompleteLocked(entry, update);
        }
    }

    private void CompleteLocked(JobEntry entry, JobUpdate update)
    {
        if (entry.Snapshot.IsTerminal)
        {
            return;
        }

        entry.Snapshot = entry.CancellationRequested ? new JobUpdate(JobState.Cancelled) : update;
        entry.CompletedAt = _timeProvider.GetUtcNow();
    }

    private static bool IsCancellationRequested(JobEntry entry)
    {
        lock (entry.Gate)
        {
            return entry.CancellationRequested;
        }
    }

    private void SweepExpired()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var (requestId, entry) in _jobs)
        {
            lock (entry.Gate)
            {
                if (entry.CompletedAt is not { } completedAt
                    || now - completedAt < TerminalRetention
                    || !_jobs.TryRemove(requestId, out var removed)
                    || !ReferenceEquals(removed, entry))
                {
                    continue;
                }

                entry.TransportCancellation.Dispose();
            }
        }
    }

    private bool IsCurrentEntry(string requestId, JobEntry entry) =>
        _jobs.TryGetValue(requestId, out var current) && ReferenceEquals(current, entry);

    private string ValidateHandle(ProviderJobHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (!string.Equals(handle.ProviderId, _providerId, StringComparison.Ordinal)
            || !Guid.TryParseExact(handle.RequestId, "N", out _)
            || !string.Equals(handle.StatusUrl, LocalUrl(handle.RequestId, "status"), StringComparison.Ordinal)
            || !string.Equals(handle.ResponseUrl, LocalUrl(handle.RequestId, "result"), StringComparison.Ordinal)
            || !string.Equals(handle.CancelUrl, LocalUrl(handle.RequestId, "cancel"), StringComparison.Ordinal))
        {
            throw new ProviderException(ErrorKind.Unknown, "The in-memory job handle is invalid.");
        }

        return handle.RequestId;
    }

    private ProviderJobHandle CreateHandle(string requestId) => new(
        _providerId,
        requestId,
        LocalUrl(requestId, "status"),
        LocalUrl(requestId, "result"),
        LocalUrl(requestId, "cancel"));

    private string LocalUrl(string requestId, string action) => $"memory://{_encodedProviderId}/{requestId}/{action}";

    private static JobUpdate Interrupted() => new(
        JobState.Failed,
        ErrorKind: ErrorKind.Interrupted,
        ErrorMessage: "The in-memory job is no longer available in this process.");

    private sealed class JobEntry(ProviderJobHandle handle)
    {
        public object Gate { get; } = new();
        public ProviderJobHandle Handle { get; } = handle;
        public CancellationTokenSource TransportCancellation { get; } = new();
        public JobUpdate Snapshot { get; set; } = new(JobState.Queued);
        public Task Work { get; set; } = Task.CompletedTask;
        public DateTimeOffset? CompletedAt { get; set; }
        public bool CancellationRequested { get; set; }
    }
}

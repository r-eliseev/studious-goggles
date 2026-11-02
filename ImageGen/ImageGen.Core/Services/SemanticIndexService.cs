using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ImageGen.Core.Domain.Expenses;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Providers;
using ImageGen.Core.Search;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Services;

/// <summary>Builds and maintains the locally stored text-embedding index after explicit user opt-in.</summary>
public sealed class SemanticIndexService : IAsyncDisposable, ISemanticIndexController, IDisposable
{
    private const int BatchSize = 64;
    public const int MaximumIndexedSources = 10_000;
    private const int RetryCount = 2;
    private const string EmbeddingProviderId = ProviderIds.OpenRouter;
    private const string EmbeddingModel = "baai/bge-m3";
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8)];
    private static readonly TimeSpan SessionRefreshDelay = TimeSpan.FromSeconds(2);

    private readonly object _gate = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _refreshes = new();
    private readonly ConcurrentDictionary<CancellationTokenSource, Task> _refreshTasks = new();
    private readonly ISearchTextSource _sources;
    private readonly ITextEmbeddingRepository _embeddings;
    private readonly IProviderRouter _providers;
    private readonly ApiKeyService _apiKeys;
    private readonly ISemanticSearchSettingsStore _settings;
    private readonly SessionChangeNotifier _changes;
    private readonly IAsyncDelay _delay;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SemanticIndexService> _logger;
    private CancellationTokenSource? _indexCancellation;
    private Task? _indexTask;
    private bool _hasStarted;
    private bool _paused;
    private bool _rescanRequested;
    private int _disposeStarted;
    private SemanticIndexStatus _status = new(false, 0, 0, SemanticIndexState.Off);

    public SemanticIndexService(
        ISearchTextSource sources,
        ITextEmbeddingRepository embeddings,
        IProviderRouter providers,
        ApiKeyService apiKeys,
        ISemanticSearchSettingsStore settings,
        SessionChangeNotifier changes,
        IAsyncDelay delay,
        TimeProvider timeProvider,
        ILogger<SemanticIndexService> logger)
    {
        _sources = sources;
        _embeddings = embeddings;
        _providers = providers;
        _apiKeys = apiKeys;
        _settings = settings;
        _changes = changes;
        _delay = delay;
        _timeProvider = timeProvider;
        _logger = logger;
        _changes.SessionChanged += OnSessionChanged;
    }

    public event EventHandler<SemanticIndexStatus>? StatusChanged;

    public async Task<int> GetSourceCountAsync(CancellationToken cancellationToken) =>
        await _sources.CountSourcesAsync(cancellationToken).ConfigureAwait(false);

    public SemanticIndexStatus GetStatusSnapshot()
    {
        lock (_gate)
        {
            return _status;
        }
    }

    public async Task<SemanticIndexStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var enabled = await _settings.GetEnabledAsync(cancellationToken).ConfigureAwait(false);
        if (!enabled)
        {
            var off = new SemanticIndexStatus(false, 0, 0, SemanticIndexState.Off);
            SetStatus(off);
            return off;
        }

        var currentSources = await _sources.ListSourcesAsync(cancellationToken).ConfigureAwait(false);
        var totalSourceCount = await _sources.CountSourcesAsync(cancellationToken).ConfigureAwait(false);
        var stored = await _embeddings.ListAllAsync(cancellationToken).ConfigureAwait(false);
        var indexed = CountCurrent(currentSources, stored);
        var status = new SemanticIndexStatus(
            true,
            indexed,
            Math.Max(0, Math.Min(currentSources.Count, MaximumIndexedSources) - indexed),
            SemanticIndexState.Ready,
                WarningCode: totalSourceCount > MaximumIndexedSources ? "limit_reached" : null);

        if (!await _apiKeys.HasKeyAsync(EmbeddingProviderId, cancellationToken).ConfigureAwait(false))
        {
            status = status with { State = SemanticIndexState.Unavailable, ErrorCode = "missing_key" };
            SetStatus(status);
            return status;
        }

        lock (_gate)
        {
            if (_indexTask is { IsCompleted: false })
            {
                status = status with { State = SemanticIndexState.Indexing };
            }
            else if (!_hasStarted && !_paused)
            {
                StartIndexingLocked();
                status = _indexTask?.IsCompleted == true
                    ? _status
                    : status with { State = SemanticIndexState.Indexing };
            }
            else if (_status.State is SemanticIndexState.Failed or SemanticIndexState.Unavailable)
            {
                status = status with { State = _status.State, ErrorCode = _status.ErrorCode };
            }
        }

        SetStatus(status);
        return status;
    }

    public async Task EnableAsync(CancellationToken cancellationToken)
    {
        await _settings.SetEnabledAsync(true, cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _paused = false;
            _hasStarted = false;
        }

        await GetStatusAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DisableAsync(CancellationToken cancellationToken)
    {
        await _settings.SetEnabledAsync(false, cancellationToken).ConfigureAwait(false);
        Task? task;
        lock (_gate)
        {
            _paused = true;
            _indexCancellation?.Cancel();
            task = _indexTask;
        }

        if (task is not null)
        {
            await IgnoreCancellationAsync(task).ConfigureAwait(false);
        }

        await _embeddings.DeleteAllAsync(cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _hasStarted = false;
            _paused = false;
        }

        SetStatus(new SemanticIndexStatus(false, 0, 0, SemanticIndexState.Off));
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Task? task;
        lock (_gate)
        {
            _paused = true;
            _indexCancellation?.Cancel();
            task = _indexTask;
        }

        if (task is not null)
        {
            await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        var status = _status with { State = SemanticIndexState.Failed, ErrorCode = "stopped" };
        SetStatus(status);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
        {
            return;
        }

        _changes.SessionChanged -= OnSessionChanged;
        foreach (var refresh in _refreshes.Values)
        {
            refresh.Cancel();
        }

        Task? task;
        lock (_gate)
        {
            _indexCancellation?.Cancel();
            task = _indexTask;
        }

        if (task is not null)
        {
            await IgnoreCancellationAsync(task).ConfigureAwait(false);
        }

        await Task.WhenAll(_refreshTasks.Values.Select(refreshTask => IgnoreCancellationAsync(refreshTask))).ConfigureAwait(false);

        _indexCancellation?.Dispose();
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private void StartIndexingLocked()
    {
        _hasStarted = true;
        _indexCancellation?.Dispose();
        _indexCancellation = new CancellationTokenSource();
        SetStatus(_status with { Enabled = true, State = SemanticIndexState.Indexing, ErrorCode = null });
        _indexTask = IndexAsync(_indexCancellation.Token);
        _ = ObserveIndexCompletionAsync(_indexTask);
    }

    private async Task IndexAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        try
        {
            await IndexCurrentSourcesAsync(cancellationToken).ConfigureAwait(false);
            var latest = await GetCountsAsync(cancellationToken).ConfigureAwait(false);
            SetStatus(new SemanticIndexStatus(true, latest.Indexed, latest.Pending, SemanticIndexState.Ready,
                WarningCode: latest.Total > MaximumIndexedSources ? "limit_reached" : null));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (_status.Enabled)
            {
                SetStatus(_status with { State = SemanticIndexState.Failed, ErrorCode = "stopped" });
            }
        }
        catch (ProviderException exception)
        {
            _logger.LogWarning("Semantic search indexing failed with provider error {ErrorKind}.", exception.Kind);
            SetStatus(_status with
            {
                Enabled = true,
                State = exception.Kind is ErrorKind.InvalidKey or ErrorKind.ModelUnavailable
                    ? SemanticIndexState.Unavailable
                    : SemanticIndexState.Failed,
                ErrorCode = exception.Kind.ToString(),
            });
        }
        catch (Exception exception)
        {
            _logger.LogError("Semantic search indexing failed: {ExceptionType}.", exception.GetType().Name);
            SetStatus(_status with { Enabled = true, State = SemanticIndexState.Failed, ErrorCode = "index_failed" });
        }
    }

    private async Task ObserveIndexCompletionAsync(Task completedIndexing)
    {
        await completedIndexing.ConfigureAwait(false);
        var rescan = false;
        lock (_gate)
        {
            if (_rescanRequested)
            {
                _rescanRequested = false;
                if (!_paused && _status.State == SemanticIndexState.Ready)
                {
                    _hasStarted = false;
                    rescan = true;
                }
            }
        }

        if (rescan)
        {
            try
            {
                await RequestIndexingAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogWarning("Semantic index rescan could not start: {ExceptionType}.", exception.GetType().Name);
            }
        }
    }

    private async Task IndexCurrentSourcesAsync(CancellationToken cancellationToken)
    {
        var allSources = await _sources.ListSourcesAsync(cancellationToken).ConfigureAwait(false);
        var totalSourceCount = await _sources.CountSourcesAsync(cancellationToken).ConfigureAwait(false);
        if (totalSourceCount > MaximumIndexedSources)
        {
            _logger.LogWarning("Semantic text index limit {MaximumIndexedSources} reached; indexing the newest sources only.", MaximumIndexedSources);
        }

        var selected = allSources.Take(MaximumIndexedSources).ToArray();
        var selectedByKey = selected.ToDictionary(source => source.SourceKey, StringComparer.Ordinal);
        var hashesByKey = selected.ToDictionary(source => source.SourceKey, source => HashText(source.Text), StringComparer.Ordinal);
        var stored = await _embeddings.ListAllAsync(cancellationToken).ConfigureAwait(false);
        var dimensions = stored.Where(embedding => embedding.Model == EmbeddingModel)
            .Select(embedding => embedding.Dimensions)
            .Distinct()
            .ToArray();
        if (dimensions.Length > 1)
        {
            await _embeddings.DeleteAllAsync(cancellationToken).ConfigureAwait(false);
            stored = [];
            dimensions = [];
        }

        int? expectedDimensions = dimensions.Length == 1 ? dimensions[0] : null;
        var byKey = stored.ToDictionary(embedding => embedding.SourceKey, StringComparer.Ordinal);

        var remove = stored
            .Where(embedding => !selectedByKey.TryGetValue(embedding.SourceKey, out var source)
                || !string.Equals(embedding.ContentHash, hashesByKey[source.SourceKey], StringComparison.Ordinal)
                || !string.Equals(embedding.Model, EmbeddingModel, StringComparison.Ordinal))
            .Select(embedding => embedding.SourceKey)
            .ToArray();
        await _embeddings.DeleteBySourceKeysAsync(remove, cancellationToken).ConfigureAwait(false);
        foreach (var key in remove)
        {
            byKey.Remove(key);
        }

        var pending = selected
            .Where(source => !byKey.ContainsKey(source.SourceKey))
            .ToArray();
        SetStatus(new SemanticIndexStatus(true, byKey.Count, Math.Max(0, selected.Length - byKey.Count), SemanticIndexState.Indexing,
            WarningCode: totalSourceCount > MaximumIndexedSources ? "limit_reached" : null));

        for (var offset = 0; offset < pending.Length; offset += BatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = pending.Skip(offset).Take(BatchSize).ToArray();
            var texts = batch.Select(source => NormalizeText(source.Text)).ToArray();
            var result = await EmbedWithRetryAsync(texts, cancellationToken).ConfigureAwait(false);
            ValidateResult(result, batch.Length);

            var expense = result.CostUsd is decimal cost
                ? new Expense
                {
                    OperationId = $"embed:{Guid.NewGuid():N}",
                    Kind = ExpenseKind.Embedding,
                    ProviderId = EmbeddingProviderId,
                    ModelId = result.Model,
                    CostUsd = cost,
                    OccurredAt = _timeProvider.GetUtcNow(),
                }
                : null;
            if (expectedDimensions is int expected && expected != result.Dimensions)
            {
                await _embeddings.UpsertBatchAsync([], expense, cancellationToken).ConfigureAwait(false);
                await _embeddings.DeleteAllAsync(cancellationToken).ConfigureAwait(false);
                throw new ProviderException(ErrorKind.Unknown, "Embedding dimension changed; the old index was cleared.");
            }

            expectedDimensions ??= result.Dimensions;

            var current = await _sources.ListSourcesByKeysAsync(batch.Select(source => source.SourceKey).ToArray(), cancellationToken).ConfigureAwait(false);
            var currentByKey = current.ToDictionary(source => source.SourceKey, StringComparer.Ordinal);
            var saved = new List<TextEmbedding>(batch.Length);
            for (var index = 0; index < batch.Length; index++)
            {
                var source = batch[index];
                if (!currentByKey.TryGetValue(source.SourceKey, out var latest)
                    || !string.Equals(hashesByKey[source.SourceKey], HashText(latest.Text), StringComparison.Ordinal))
                {
                    continue;
                }

                saved.Add(new TextEmbedding(
                    source.SourceKey,
                    source.SessionId,
                    source.Kind,
                    hashesByKey[source.SourceKey],
                    result.Model,
                    result.Dimensions,
                    result.Vectors[index],
                    _timeProvider.GetUtcNow()));
            }

            await _embeddings.UpsertBatchAsync(saved, expense, cancellationToken).ConfigureAwait(false);
            SetStatus(new SemanticIndexStatus(true, byKey.Count + saved.Count, Math.Max(0, selected.Length - byKey.Count - saved.Count),
                SemanticIndexState.Indexing, WarningCode: totalSourceCount > MaximumIndexedSources ? "limit_reached" : null));
        }
    }

    private async Task<EmbeddingResult> EmbedWithRetryAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        var client = _providers.GetTextEmbeddingClient(EmbeddingProviderId);
        for (var retry = 0; ; retry++)
        {
            try
            {
                return await client.EmbedAsync(texts, cancellationToken).ConfigureAwait(false);
            }
            catch (ProviderException exception) when (retry < RetryCount && IsTransient(exception))
            {
                await _delay.DelayAsync(RetryDelays[retry], cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task RequestIndexingAsync(CancellationToken cancellationToken)
    {
        if (!await _settings.GetEnabledAsync(cancellationToken).ConfigureAwait(false)
            || !await _apiKeys.HasKeyAsync(EmbeddingProviderId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        lock (_gate)
        {
            if (_indexTask is { IsCompleted: false })
            {
                _rescanRequested = true;
                return;
            }

            _paused = false;
            _hasStarted = false;
            StartIndexingLocked();
        }
    }

    private void OnSessionChanged(object? sender, Guid sessionId)
    {
        var cancellation = new CancellationTokenSource();
        if (_refreshes.TryGetValue(sessionId, out var previous))
        {
            previous.Cancel();
        }

        _refreshes[sessionId] = cancellation;
        _refreshTasks[cancellation] = RefreshAfterDelayAsync(sessionId, cancellation);
    }

    private async Task RefreshAfterDelayAsync(Guid sessionId, CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Yield();
            await _delay.DelayAsync(SessionRefreshDelay, cancellation.Token).ConfigureAwait(false);
            await RequestIndexingAsync(cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Semantic search refresh failed for session {SessionId}: {ExceptionType}.", sessionId, exception.GetType().Name);
        }
        finally
        {
            if (_refreshes.TryGetValue(sessionId, out var current) && ReferenceEquals(current, cancellation))
            {
                _refreshes.TryRemove(sessionId, out _);
            }

            cancellation.Dispose();
            _refreshTasks.TryRemove(cancellation, out _);
        }
    }

    private async Task<(int Indexed, int Pending, int Total)> GetCountsAsync(CancellationToken cancellationToken)
    {
        var sources = await _sources.ListSourcesAsync(cancellationToken).ConfigureAwait(false);
        var totalSourceCount = await _sources.CountSourcesAsync(cancellationToken).ConfigureAwait(false);
        var embeddings = await _embeddings.ListAllAsync(cancellationToken).ConfigureAwait(false);
        var indexed = CountCurrent(sources, embeddings);
        return (indexed, Math.Max(0, Math.Min(totalSourceCount, MaximumIndexedSources) - indexed), totalSourceCount);
    }

    private static int CountCurrent(IReadOnlyList<SearchSource> sources, IReadOnlyList<TextEmbedding> embeddings)
    {
        var stored = embeddings.ToDictionary(embedding => embedding.SourceKey, StringComparer.Ordinal);
        return sources.Take(MaximumIndexedSources).Count(source =>
            stored.TryGetValue(source.SourceKey, out var embedding)
            && string.Equals(embedding.ContentHash, HashText(source.Text), StringComparison.Ordinal)
            && string.Equals(embedding.Model, EmbeddingModel, StringComparison.Ordinal));
    }

    private void SetStatus(SemanticIndexStatus status)
    {
        EventHandler<SemanticIndexStatus>? handlers;
        lock (_gate)
        {
            if (_status == status)
            {
                return;
            }

            _status = status;
            handlers = StatusChanged;
        }

        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler<SemanticIndexStatus> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, status);
            }
            catch (Exception exception)
            {
                _logger.LogWarning("Semantic index status subscriber failed: {ExceptionType}.", exception.GetType().Name);
            }
        }
    }

    private static string NormalizeText(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string HashText(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeText(text))));

    private static bool IsTransient(ProviderException exception) => exception.Kind is
        ErrorKind.Network or ErrorKind.Timeout or ErrorKind.RateLimited
        || exception.StatusCode is >= 500;

    private static void ValidateResult(EmbeddingResult result, int expectedCount)
    {
        if (result.Vectors.Length != expectedCount || result.Dimensions <= 0 || result.Model != EmbeddingModel)
        {
            throw new ProviderException(ErrorKind.Unknown, "Embedding response did not match the requested batch.");
        }

        foreach (var vector in result.Vectors)
        {
            if (vector.Length != result.Dimensions || vector.Any(value => !float.IsFinite(value)))
            {
                throw new ProviderException(ErrorKind.Unknown, "Embedding response contained an invalid vector.");
            }
        }
    }

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
}

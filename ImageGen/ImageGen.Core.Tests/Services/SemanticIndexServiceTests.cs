using ImageGen.Core.Domain.Expenses;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Providers;
using ImageGen.Core.Search;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ImageGen.Core.Tests.Services;

public class SemanticIndexServiceTests
{
    [Fact]
    public async Task EnableAsync_EmbedsOnlyNormalizedSourcesAndPersistsKnownCost()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = Source(SearchTextKind.Title, "  red   fox ");
        var setup = Create([source]);
        setup.Client.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new EmbeddingResult([[0.5f, 0.25f]], "baai/bge-m3", 2, 0.02m));

        await setup.Service.EnableAsync(ct);
        var status = await WaitForStateAsync(setup.Service, SemanticIndexState.Ready, ct);

        Assert.Equal(1, status.Indexed);
        Assert.Equal(0, status.Pending);
        await setup.Client.Received(1).EmbedAsync(
            Arg.Is<IReadOnlyList<string>>(texts => texts.SequenceEqual(new[] { "red fox" })), Arg.Any<CancellationToken>());
        var saved = Assert.Single(setup.Repository.Items.Values);
        Assert.Equal(source.SourceKey, saved.SourceKey);
        Assert.Equal("baai/bge-m3", saved.Model);
        Assert.Equal(ExpenseKind.Embedding, Assert.Single(setup.Repository.Expenses).Kind);
        Assert.Equal(0.02m, setup.Repository.Expenses[0].CostUsd);

        await setup.Service.DisposeAsync();
    }

    [Fact]
    public async Task GetStatusAsync_AfterReady_DoesNotEmbedUnchangedTextAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = Create([Source(SearchTextKind.Prompt, "a small house")]);
        setup.Client.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(Result(call.Arg<IReadOnlyList<string>>().Count)));

        await setup.Service.EnableAsync(ct);
        await WaitForStateAsync(setup.Service, SemanticIndexState.Ready, ct);
        var status = await setup.Service.GetStatusAsync(ct);

        Assert.Equal(SemanticIndexState.Ready, status.State);
        await setup.Client.Received(1).EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        await setup.Service.DisposeAsync();
    }

    [Fact]
    public async Task EnableAsync_WithoutOpenRouterKey_ReportsUnavailableWithoutCallingProvider()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = Create([Source(SearchTextKind.Title, "fox")], hasKey: false);

        await setup.Service.EnableAsync(ct);
        var status = await setup.Service.GetStatusAsync(ct);

        Assert.Equal(SemanticIndexState.Unavailable, status.State);
        Assert.Equal("missing_key", status.ErrorCode);
        await setup.Client.DidNotReceive().EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        await setup.Service.DisposeAsync();
    }

    [Fact]
    public async Task IndexAsync_SourceDeletedDuringProviderCall_DoesNotPersistStaleVector()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = Source(SearchTextKind.Description, "mountain lake");
        var setup = Create([source]);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        setup.Client.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(call.Arg<CancellationToken>());
                return new EmbeddingResult([[1f, 0f]], "baai/bge-m3", 2, null);
            });

        await setup.Service.EnableAsync(ct);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        setup.Sources.Items = [];
        release.TrySetResult();
        await WaitForStateAsync(setup.Service, SemanticIndexState.Ready, ct);

        Assert.Empty(setup.Repository.Items);
        await setup.Service.DisposeAsync();
    }

    [Fact]
    public async Task IndexAsync_ManyBatches_DoesNotReloadAllSourcesPerBatch()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = Create(Enumerable.Range(0, 200).Select(index => Source(SearchTextKind.Prompt, $"source {index}")).ToArray());
        var readsAtReady = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        setup.Service.StatusChanged += (_, status) =>
        {
            if (status.State == SemanticIndexState.Ready)
            {
                readsAtReady.TrySetResult(setup.Sources.ListSourcesAsyncCalls);
            }
        };
        setup.Client.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(Result(call.Arg<IReadOnlyList<string>>().Count)));

        await setup.Service.EnableAsync(ct);
        var reads = await readsAtReady.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        var status = setup.Service.GetStatusSnapshot();

        Assert.Equal(200, status.Indexed);
        Assert.InRange(reads, 1, 3);
        await setup.Service.DisposeAsync();
    }

    [Fact]
    public async Task IndexAsync_NetworkFailure_RetriesTwiceWithPlannedDelays()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = Create([Source(SearchTextKind.Prompt, "red fox")]);
        var attempts = 0;
        setup.Client.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (Interlocked.Increment(ref attempts) < 3)
                {
                    return Task.FromException<EmbeddingResult>(new ProviderException(ErrorKind.Network, "network"));
                }

                return Task.FromResult(Result(call.Arg<IReadOnlyList<string>>().Count));
            });

        await setup.Service.EnableAsync(ct);
        await WaitForStateAsync(setup.Service, SemanticIndexState.Ready, ct);

        Assert.Equal(3, attempts);
        Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8)], setup.Delay.Delays);
        await setup.Service.DisposeAsync();
    }

    [Fact]
    public async Task IndexAsync_MissingModelDoesNotRetryDespiteSharedErrorKind()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = Create([Source(SearchTextKind.Title, "fox")]);
        setup.Client.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<EmbeddingResult>(
                new ProviderException(ErrorKind.ModelUnavailable, "missing model", statusCode: 404)));

        await setup.Service.EnableAsync(ct);
        var status = await WaitForStateAsync(setup.Service, SemanticIndexState.Unavailable, ct);

        Assert.Equal("ModelUnavailable", status.ErrorCode);
        await setup.Client.Received(1).EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        Assert.Empty(setup.Delay.Delays);
        await setup.Service.DisposeAsync();
    }

    [Fact]
    public async Task SessionChanged_ChangedTextReplacesItsOldVector()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = Source(SearchTextKind.Title, "first version");
        var setup = Create([source]);
        var calls = 0;
        setup.Client.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult(Result(call.Arg<IReadOnlyList<string>>().Count));
            });

        await setup.Service.EnableAsync(ct);
        await WaitForStateAsync(setup.Service, SemanticIndexState.Ready, ct);
        setup.Sources.Items = [source with { Text = "second version" }];
        setup.Changes.Notify(source.SessionId);
        await WaitForAsync(() => calls == 2, ct);
        await WaitForStateAsync(setup.Service, SemanticIndexState.Ready, ct);

        var saved = Assert.Single(setup.Repository.Items.Values);
        Assert.NotEqual(Hash("first version"), saved.ContentHash);
        Assert.Equal(Hash("second version"), saved.ContentHash);
        await setup.Service.DisposeAsync();
    }

    [Fact]
    public async Task StopAsync_PreservesCompletedBatchesAndEnableResumesRemainingSources()
    {
        var ct = TestContext.Current.CancellationToken;
        var sources = Enumerable.Range(0, 65)
            .Select(index => new SearchSource($"prompt:{index}", Guid.NewGuid(), SearchTextKind.Prompt, $"prompt {index}"))
            .ToArray();
        var setup = Create(sources);
        var attempts = 0;
        var secondBatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        setup.Client.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var attempt = Interlocked.Increment(ref attempts);
                if (attempt == 2)
                {
                    secondBatchStarted.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>());
                }

                return Result(call.Arg<IReadOnlyList<string>>().Count);
            });

        await setup.Service.EnableAsync(ct);
        await secondBatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        await setup.Service.StopAsync(ct);

        Assert.Equal(64, setup.Repository.Items.Count);
        Assert.Equal("stopped", (await setup.Service.GetStatusAsync(ct)).ErrorCode);

        await setup.Service.EnableAsync(ct);
        var status = await WaitForStateAsync(setup.Service, SemanticIndexState.Ready, ct);

        Assert.Equal(65, status.Indexed);
        Assert.Equal(3, attempts);
        await setup.Service.DisposeAsync();
    }

    [Fact]
    public async Task IndexAsync_NonFiniteProviderVector_FailsWithoutPersistingIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = Create([Source(SearchTextKind.Title, "finite only")]);
        setup.Client.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new EmbeddingResult([[float.NaN, 1f]], "baai/bge-m3", 2, null));

        await setup.Service.EnableAsync(ct);
        var status = await WaitForStateAsync(setup.Service, SemanticIndexState.Failed, ct);

        Assert.Equal("Unknown", status.ErrorCode);
        Assert.Equal("vector 0 contains a non-finite value", status.ErrorDetail);
        Assert.Empty(setup.Repository.Items);
        await setup.Service.DisposeAsync();
    }

    [Fact]
    public async Task IndexAsync_InvalidResponse_StillRecordsPaidExpense()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = Create([Source(SearchTextKind.Title, "finite only")]);
        setup.Client.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new EmbeddingResult([[float.NaN, 1f]], "baai/bge-m3", 2, 0.001m));

        await setup.Service.EnableAsync(ct);
        var status = await WaitForStateAsync(setup.Service, SemanticIndexState.Failed, ct);

        Assert.Equal("Unknown", status.ErrorCode);
        Assert.Empty(setup.Repository.Items);
        Assert.Equal(0.001m, Assert.Single(setup.Repository.Expenses).CostUsd);
        await setup.Service.DisposeAsync();
    }

    [Fact]
    public async Task IndexAsync_ProviderModelNameStoresRequestedModelAndDoesNotReindex()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = Create([Source(SearchTextKind.Title, "model mismatch")]);
        setup.Client.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new EmbeddingResult([[0.5f]], "parasail-bge-m3", 1, 0.00000016m));

        await setup.Service.EnableAsync(ct);
        var status = await WaitForStateAsync(setup.Service, SemanticIndexState.Ready, ct);

        Assert.Equal(SemanticIndexState.Ready, status.State);
        Assert.Equal("baai/bge-m3", Assert.Single(setup.Repository.Items.Values).Model);
        Assert.Equal("baai/bge-m3", Assert.Single(setup.Repository.Expenses).ModelId);
        await setup.Service.GetStatusAsync(ct);
        await setup.Client.Received(1).EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        await setup.Service.DisposeAsync();
    }

    [Fact]
    public async Task IndexAsync_ModelNameComparisonIsCaseInsensitiveAndStoresResponseName()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = Create([Source(SearchTextKind.Title, "model casing")]);
        setup.Client.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new EmbeddingResult([[0.5f]], "BAAI/BGE-M3", 1, null));

        await setup.Service.EnableAsync(ct);
        var status = await WaitForStateAsync(setup.Service, SemanticIndexState.Ready, ct);

        Assert.Equal(SemanticIndexState.Ready, status.State);
        Assert.Equal("baai/bge-m3", Assert.Single(setup.Repository.Items.Values).Model);
        await setup.Service.DisposeAsync();
    }

    [Fact]
    public async Task IndexAsync_ChangedVectorDimensionClearsMixedIndexAndCanBeRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        var sources = Enumerable.Range(0, 65)
            .Select(index => new SearchSource($"prompt:{index}", Guid.NewGuid(), SearchTextKind.Prompt, $"prompt {index}"))
            .ToArray();
        var setup = Create(sources);
        var attempts = 0;
        setup.Client.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var attempt = Interlocked.Increment(ref attempts);
                var dimensions = attempt == 2 ? 3 : 2;
                var count = call.Arg<IReadOnlyList<string>>().Count;
                return Task.FromResult(new EmbeddingResult(
                    Enumerable.Range(0, count).Select(_ => Enumerable.Repeat(1f, dimensions).ToArray()).ToArray(),
                    "baai/bge-m3", dimensions, 0.01m));
            });

        await setup.Service.EnableAsync(ct);
        var failed = await WaitForStateAsync(setup.Service, SemanticIndexState.Failed, ct);

        Assert.Equal("Unknown", failed.ErrorCode);
        Assert.Equal("embedding dimension changed from 2 to 3; the old index was cleared", failed.ErrorDetail);
        Assert.Empty(setup.Repository.Items);
        Assert.Equal(2, setup.Repository.Expenses.Count);

        await setup.Service.EnableAsync(ct);
        var ready = await WaitForStateAsync(setup.Service, SemanticIndexState.Ready, ct);

        Assert.Equal(65, ready.Indexed);
        Assert.All(setup.Repository.Items.Values, embedding => Assert.Equal(2, embedding.Dimensions));
        await setup.Service.DisposeAsync();
    }

    [Fact]
    public async Task EnableAsync_IndexesOnlyTenThousandMostRecentSourcesInBoundedBatches()
    {
        var ct = TestContext.Current.CancellationToken;
        var sources = Enumerable.Range(0, 10_001)
            .Select(index => new SearchSource($"title:{index}", Guid.NewGuid(), SearchTextKind.Title, $"title {index}"))
            .ToArray();
        var setup = Create(sources);
        var calls = 0;
        var batchSizes = new List<int>();
        setup.Client.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Interlocked.Increment(ref calls);
                var count = call.Arg<IReadOnlyList<string>>().Count;
                batchSizes.Add(count);
                return Task.FromResult(Result(count));
            });

        await setup.Service.EnableAsync(ct);
        var status = await WaitForStateAsync(setup.Service, SemanticIndexState.Ready, ct);

        Assert.Equal(10_000, status.Indexed);
        Assert.Equal(0, status.Pending);
        Assert.Equal("limit_reached", status.WarningCode);
        Assert.Equal(157, calls);
        Assert.All(batchSizes, size => Assert.InRange(size, 1, 64));
        Assert.Equal(16, batchSizes[^1]);
        await setup.Service.DisposeAsync();
    }

    [Fact]
    public async Task GetStatusAsync_StaleSnapshotAfterIndexCompletes_ReturnsFinalStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = Create(Enumerable.Range(0, 128)
            .Select(index => new SearchSource($"title:{index}", Guid.NewGuid(), SearchTextKind.Title, $"title {index}"))
            .ToArray());
        var firstBatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstBatch = new TaskCompletionSource<EmbeddingResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        setup.Client.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var count = call.Arg<IReadOnlyList<string>>().Count;
                if (Interlocked.Increment(ref calls) == 1)
                {
                    firstBatchStarted.TrySetResult();
                    return releaseFirstBatch.Task;
                }

                return Task.FromResult(Result(count));
            });

        await setup.Service.EnableAsync(ct);
        await firstBatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        var (snapshotCaptured, releaseSnapshot) = setup.Repository.BlockNextListAll();
        var staleStatusTask = setup.Service.GetStatusAsync(ct);
        await snapshotCaptured.WaitAsync(TimeSpan.FromSeconds(5), ct);

        releaseFirstBatch.SetResult(Result(64));
        await WaitForAsync(() => setup.Service.GetStatusSnapshot().State == SemanticIndexState.Ready, ct);
        releaseSnapshot();

        var status = await staleStatusTask;

        Assert.Equal(SemanticIndexState.Ready, status.State);
        Assert.Equal(128, status.Indexed);
        Assert.Equal(0, status.Pending);
        await setup.Service.DisposeAsync();
    }

    private static (SemanticIndexService Service, MemorySources Sources, MemoryEmbeddings Repository, ITextEmbeddingClient Client, SessionChangeNotifier Changes, RecordingDelay Delay) Create(
        IReadOnlyList<SearchSource> sources,
        bool hasKey = true)
    {
        var sourceStore = new MemorySources(sources);
        var repository = new MemoryEmbeddings();
        var client = Substitute.For<ITextEmbeddingClient>();
        var router = Substitute.For<IProviderRouter>();
        router.GetTextEmbeddingClient(ProviderIds.OpenRouter).Returns(client);
        var secrets = Substitute.For<ImageGen.Core.Platform.ISecretStore>();
        secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns(hasKey ? "test-key" : null);
        var settings = Substitute.For<ISemanticSearchSettingsStore>();
        var enabled = false;
        settings.GetEnabledAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(enabled));
        settings.SetEnabledAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            enabled = call.Arg<bool>();
            return Task.CompletedTask;
        });
        var delay = new RecordingDelay();
        var changes = new SessionChangeNotifier(NullLogger<SessionChangeNotifier>.Instance);
        var apiKeys = new ApiKeyService(router, secrets, NullLogger<ApiKeyService>.Instance);
        var service = new SemanticIndexService(
            sourceStore,
            repository,
            router,
            apiKeys,
            settings,
            changes,
            delay,
            TimeProvider.System,
            NullLogger<SemanticIndexService>.Instance);
        return (service, sourceStore, repository, client, changes, delay);
    }

    private static SearchSource Source(SearchTextKind kind, string text)
    {
        var sessionId = Guid.NewGuid();
        return new SearchSource($"{kind.ToString().ToLowerInvariant()}:{Guid.NewGuid():D}", sessionId, kind, text);
    }

    private static EmbeddingResult Result(int count) => new(
        Enumerable.Range(0, count).Select(_ => new[] { 1f, 0f }).ToArray(), "baai/bge-m3", 2, 0.01m);

    private static string Hash(string value) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    private static async Task<SemanticIndexStatus> WaitForStateAsync(
        SemanticIndexService service,
        SemanticIndexState state,
        CancellationToken cancellationToken)
    {
        var limit = DateTimeOffset.UtcNow.AddSeconds(10);
        SemanticIndexStatus? lastStatus = null;
        while (DateTimeOffset.UtcNow < limit)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lastStatus = await service.GetStatusAsync(cancellationToken);
            if (lastStatus.State == state)
            {
                return lastStatus;
            }

            await Task.Delay(10, cancellationToken);
        }

        throw new TimeoutException($"Semantic index did not reach {state} state. Last status: {lastStatus}.");
    }

    private static async Task WaitForAsync(Func<bool> predicate, CancellationToken cancellationToken)
    {
        var limit = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!predicate() && DateTimeOffset.UtcNow < limit)
        {
            await Task.Delay(10, cancellationToken);
        }

        Assert.True(predicate(), "The requested condition did not become true in time.");
    }

    private sealed class MemorySources(IReadOnlyList<SearchSource> items) : ISearchTextSource
    {
        public IReadOnlyList<SearchSource> Items { get; set; } = items;
        public int ListSourcesAsyncCalls { get; private set; }

        public Task<IReadOnlyList<SearchText>> ListAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SearchText>>([]);

        public Task<IReadOnlyList<SearchSource>> ListSourcesAsync(CancellationToken cancellationToken)
        {
            ListSourcesAsyncCalls++;
            return Task.FromResult(Items);
        }

        public Task<IReadOnlyList<SearchSource>> ListSourcesByKeysAsync(IReadOnlyCollection<string> sourceKeys, CancellationToken cancellationToken)
        {
            var keys = sourceKeys.ToHashSet(StringComparer.Ordinal);
            return Task.FromResult<IReadOnlyList<SearchSource>>(Items.Where(item => keys.Contains(item.SourceKey)).ToArray());
        }

        public Task<int> CountSourcesAsync(CancellationToken cancellationToken) => Task.FromResult(Items.Count);
    }

    private sealed class MemoryEmbeddings : ITextEmbeddingRepository
    {
        private readonly object _gate = new();
        private (TaskCompletionSource Captured, TaskCompletionSource Release)? _nextListAllBlock;
        public Dictionary<string, TextEmbedding> Items { get; } = new(StringComparer.Ordinal);

        public List<Expense> Expenses { get; } = [];

        public Task<IReadOnlyList<TextEmbedding>> ListAllAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<TextEmbedding> snapshot;
            (TaskCompletionSource Captured, TaskCompletionSource Release)? block;
            lock (_gate)
            {
                snapshot = Items.Values.ToArray();
                block = _nextListAllBlock;
                _nextListAllBlock = null;
            }

            return block is { } pending
                ? WaitForSnapshotReleaseAsync(snapshot, pending.Captured, pending.Release)
                : Task.FromResult(snapshot);
        }

        public (Task SnapshotCaptured, Action Release) BlockNextListAll()
        {
            var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
            {
                _nextListAllBlock = (captured, release);
            }

            return (captured.Task, () => release.TrySetResult());
        }

        private static async Task<IReadOnlyList<TextEmbedding>> WaitForSnapshotReleaseAsync(
            IReadOnlyList<TextEmbedding> snapshot,
            TaskCompletionSource captured,
            TaskCompletionSource release)
        {
            captured.TrySetResult();
            await release.Task;
            return snapshot;
        }

        public Task UpsertBatchAsync(IReadOnlyList<TextEmbedding> embeddings, Expense? expense, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                foreach (var embedding in embeddings)
                {
                    Items[embedding.SourceKey] = embedding;
                }

                if (expense is not null)
                {
                    Expenses.Add(expense);
                }
            }

            return Task.CompletedTask;
        }

        public Task DeleteBySourceKeysAsync(IReadOnlyCollection<string> sourceKeys, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                foreach (var key in sourceKeys)
                {
                    Items.Remove(key);
                }
            }

            return Task.CompletedTask;
        }

        public Task DeleteAllAsync(CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                Items.Clear();
            }
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDelay : IAsyncDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}

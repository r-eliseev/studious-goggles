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

/// <summary>Ranks locally indexed texts by cosine similarity, embedding only the user's query.</summary>
public sealed class SemanticSearchService : IDisposable
{
    private const int CacheCapacity = 50;
    private const int MaximumQueryLength = 8_192;
    private const string EmbeddingProviderId = ProviderIds.OpenRouter;
    private readonly ISearchTextSource _sources;
    private readonly ITextEmbeddingRepository _embeddings;
    private readonly IProviderRouter _providers;
    private readonly ISemanticIndexStatusProvider _index;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SemanticSearchService> _logger;
    private readonly SemaphoreSlim _searchGate = new(1, 1);
    private readonly object _cacheGate = new();
    private readonly LinkedList<(string Key, float[] Vector)> _queryCache = new();
    private IReadOnlyList<TextEmbedding>? _indexCache;

    public string? LastErrorCode { get; private set; }

    public SemanticSearchService(
        ISearchTextSource sources,
        ITextEmbeddingRepository embeddings,
        IProviderRouter providers,
        ISemanticIndexStatusProvider index,
        TimeProvider timeProvider,
        ILogger<SemanticSearchService> logger)
    {
        _sources = sources;
        _embeddings = embeddings;
        _providers = providers;
        _index = index;
        _timeProvider = timeProvider;
        _logger = logger;
        _index.StatusChanged += OnIndexStatusChanged;
    }

    /// <summary>Returns at most twenty session hits. Queries shorter than three characters are free and empty.</summary>
    public async Task<IReadOnlyList<SessionSearchHit>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedQuery = NormalizeText(query);
        if (normalizedQuery.Length < 3)
        {
            return [];
        }

        if (normalizedQuery.Length > MaximumQueryLength)
        {
            LastErrorCode = "query_too_long";
            return [];
        }

        await _searchGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LastErrorCode = null;
            var status = _index.GetStatusSnapshot();
            if (status.State != SemanticIndexState.Ready || !status.Enabled)
            {
                LastErrorCode = status.ErrorCode ?? status.State.ToString();
                return [];
            }

            if (status.Indexed == 0)
            {
                return [];
            }

            var queryVector = GetCachedQuery(normalizedQuery);
            if (queryVector is null)
            {
                var response = await _providers.GetTextEmbeddingClient(EmbeddingProviderId)
                    .EmbedAsync([normalizedQuery], cancellationToken).ConfigureAwait(false);
                ValidateQueryVector(response);
                queryVector = response.Vectors[0];
                if (response.CostUsd is decimal cost)
                {
                    await _embeddings.UpsertBatchAsync([], new Expense
                    {
                        OperationId = $"embed-query:{Guid.NewGuid():N}",
                        Kind = ExpenseKind.Embedding,
                        ProviderId = EmbeddingProviderId,
                        ModelId = response.Model,
                        CostUsd = cost,
                        OccurredAt = _timeProvider.GetUtcNow(),
                    }, cancellationToken).ConfigureAwait(false);
                }

                AddCachedQuery(normalizedQuery, queryVector);
            }

            var texts = await _sources.ListSourcesAsync(cancellationToken).ConfigureAwait(false);
            var stored = await GetIndexAsync(cancellationToken).ConfigureAwait(false);
            var indexedDimension = stored.FirstOrDefault(embedding => embedding.Model == "baai/bge-m3")?.Dimensions;
            if (indexedDimension is int dimensions && dimensions != queryVector.Length)
            {
                LastErrorCode = "dimension_mismatch";
                lock (_cacheGate)
                {
                    _queryCache.Clear();
                }

                return [];
            }

            var embeddingsByKey = stored.ToDictionary(item => item.SourceKey, StringComparer.Ordinal);
            var bestBySession = new Dictionary<Guid, (SessionSearchHit Hit, int SourceOrder)>();
            for (var order = 0; order < texts.Count; order++)
            {
                var source = texts[order];
                if (!embeddingsByKey.TryGetValue(source.SourceKey, out var embedding)
                    || embedding.SessionId != source.SessionId
                    || embedding.Model != "baai/bge-m3"
                    || embedding.ContentHash != HashText(source.Text)
                    || embedding.Dimensions != queryVector.Length
                    || embedding.Vector.Length != queryVector.Length)
                {
                    continue;
                }

                var similarity = CosineSimilarity(queryVector, embedding.Vector);
                if (similarity is null)
                {
                    continue;
                }

                var hit = new SessionSearchHit(source.SessionId, (int)Math.Round(similarity.Value * 10_000), source.Kind, Snippet(source.Text));
                if (!bestBySession.TryGetValue(source.SessionId, out var previous) || hit.Score > previous.Hit.Score)
                {
                    bestBySession[source.SessionId] = (hit, order);
                }
            }

            return bestBySession.Values
                .OrderByDescending(item => item.Hit.Score)
                .ThenBy(item => item.SourceOrder)
                .Take(20)
                .Select(item => item.Hit)
                .ToArray();
        }
        catch (ProviderException exception)
        {
            LastErrorCode = exception.Kind.ToString();
            _logger.LogWarning("Semantic query failed with provider error {ErrorKind}.", exception.Kind);
            return [];
        }
        finally
        {
            _searchGate.Release();
        }
    }

    public void Dispose()
    {
        _index.StatusChanged -= OnIndexStatusChanged;
        _searchGate.Dispose();
    }

    private void OnIndexStatusChanged(object? sender, SemanticIndexStatus status)
    {
        lock (_cacheGate)
        {
            _indexCache = null;
        }
    }

    private async Task<IReadOnlyList<TextEmbedding>> GetIndexAsync(CancellationToken cancellationToken)
    {
        lock (_cacheGate)
        {
            if (_indexCache is not null)
            {
                return _indexCache;
            }
        }

        var loaded = await _embeddings.ListAllAsync(cancellationToken).ConfigureAwait(false);
        lock (_cacheGate)
        {
            _indexCache ??= loaded;
            return _indexCache;
        }
    }

    private float[]? GetCachedQuery(string query)
    {
        var key = $"baai/bge-m3\n{query}";
        lock (_cacheGate)
        {
            var node = _queryCache.First;
            while (node is not null)
            {
                if (node.Value.Key == key)
                {
                    _queryCache.Remove(node);
                    _queryCache.AddFirst(node);
                    return node.Value.Vector;
                }

                node = node.Next;
            }
        }

        return null;
    }

    private void AddCachedQuery(string query, float[] vector)
    {
        var key = $"baai/bge-m3\n{query}";
        lock (_cacheGate)
        {
            _queryCache.AddFirst((key, vector));
            if (_queryCache.Count > CacheCapacity)
            {
                _queryCache.RemoveLast();
            }
        }
    }

    private static void ValidateQueryVector(EmbeddingResult result)
    {
        if (result.Vectors.Length != 1 || result.Model != "baai/bge-m3" || result.Dimensions <= 0 || result.Vectors[0].Length != result.Dimensions
            || result.Vectors[0].Any(value => !float.IsFinite(value)))
        {
            throw new ProviderException(ErrorKind.Unknown, "Embedding response contained an invalid query vector.");
        }
    }

    private static double? CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;
        for (var index = 0; index < left.Count; index++)
        {
            dot += left[index] * right[index];
            leftNorm += left[index] * left[index];
            rightNorm += right[index] * right[index];
        }

        if (leftNorm == 0 || rightNorm == 0)
        {
            return null;
        }

        return dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
    }

    private static string NormalizeText(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string HashText(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeText(text))));

    private static string Snippet(string text)
    {
        var normalized = NormalizeText(text);
        return normalized.Length <= 80 ? normalized : $"{normalized[..77]}…";
    }
}

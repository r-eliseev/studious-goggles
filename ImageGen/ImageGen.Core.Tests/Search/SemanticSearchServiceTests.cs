using ImageGen.Core.Domain.Expenses;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Providers;
using ImageGen.Core.Search;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ImageGen.Core.Tests.Search;

public sealed class SemanticSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_RanksCosineSimilarityAndUsesLocalSnippet()
    {
        var ct = TestContext.Current.CancellationToken;
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();
        var sources = new[]
        {
            new SearchSource("title:a", sessionA, SearchTextKind.Title, "North star observatory"),
            new SearchSource("prompt:b", sessionB, SearchTextKind.Prompt, "A red fox in snow"),
        };
        var setup = Create(sources,
        [
            Embedding("title:a", sessionA, SearchTextKind.Title, sources[0].Text, [0.8f, 0.6f]),
            Embedding("prompt:b", sessionB, SearchTextKind.Prompt, sources[1].Text, [0.1f, 0.995f]),
        ]);
        setup.Client.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new EmbeddingResult([[1f, 0f]], "baai/bge-m3", 2, null));

        var hits = await setup.Service.SearchAsync("  northern   sky ", ct);

        Assert.Equal(sessionA, hits[0].SessionId);
        Assert.Equal("North star observatory", hits[0].Snippet);
        Assert.True(hits[0].Score > hits[1].Score);
        await setup.Client.Received(1).EmbedAsync(
            Arg.Is<IReadOnlyList<string>>(texts => texts.SequenceEqual(new[] { "northern sky" })), Arg.Any<CancellationToken>());
        setup.Service.Dispose();
    }

    [Fact]
    public async Task SearchAsync_ShortQueryOrUnavailableIndex_DoesNotCallProvider()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = new SearchSource("title:a", Guid.NewGuid(), SearchTextKind.Title, "fox");
        var setup = Create([source], [], ready: false);

        Assert.Empty(await setup.Service.SearchAsync("ab", ct));
        Assert.Empty(await setup.Service.SearchAsync("fox", ct));
        await setup.Client.DidNotReceive().EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        setup.Service.Dispose();
    }

    [Fact]
    public async Task SearchAsync_OversizedQueryIsRejectedWithoutProviderCall()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = new SearchSource("title:a", Guid.NewGuid(), SearchTextKind.Title, "blue lake");
        var setup = Create([source], [Embedding("title:a", source.SessionId, SearchTextKind.Title, source.Text, [1f, 0f])]);

        Assert.Empty(await setup.Service.SearchAsync(new string('x', 8_193), ct));

        Assert.Equal("query_too_long", setup.Service.LastErrorCode);
        await setup.Client.DidNotReceive().EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        setup.Service.Dispose();
    }

    [Fact]
    public async Task SearchAsync_CachesQueryEmbedding()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = new SearchSource("title:a", Guid.NewGuid(), SearchTextKind.Title, "blue lake");
        var setup = Create([source], [Embedding("title:a", source.SessionId, SearchTextKind.Title, source.Text, [1f, 0f])]);
        setup.Client.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new EmbeddingResult([[1f, 0f]], "baai/bge-m3", 2, null));

        await setup.Service.SearchAsync("blue lake", ct);
        await setup.Service.SearchAsync("blue lake", ct);

        await setup.Client.Received(1).EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        setup.Service.Dispose();
    }

    [Fact]
    public async Task SearchAsync_RecordsKnownQueryCostAndReportsProviderFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = new SearchSource("title:a", Guid.NewGuid(), SearchTextKind.Title, "blue lake");
        var setup = Create([source], [Embedding("title:a", source.SessionId, SearchTextKind.Title, source.Text, [1f, 0f])]);
        setup.Client.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new EmbeddingResult([[1f, 0f]], "baai/bge-m3", 2, 0.03m));

        await setup.Service.SearchAsync("blue lake", ct);

        await setup.Repository.Received(1).UpsertBatchAsync([], Arg.Is<Expense?>(expense =>
            expense != null && expense.Kind == ExpenseKind.Embedding && expense.CostUsd == 0.03m), Arg.Any<CancellationToken>());
        setup.Service.Dispose();

        var failed = Create([source], [Embedding("title:a", source.SessionId, SearchTextKind.Title, source.Text, [1f, 0f])]);
        failed.Client.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<EmbeddingResult>(new ProviderException(ErrorKind.Network, "offline")));

        Assert.Empty(await failed.Service.SearchAsync("blue lake", ct));
        Assert.Equal(ErrorKind.Network.ToString(), failed.Service.LastErrorCode);
        failed.Service.Dispose();
    }

    private static (SemanticSearchService Service, ITextEmbeddingClient Client, ITextEmbeddingRepository Repository) Create(
        IReadOnlyList<SearchSource> sources,
        IReadOnlyList<TextEmbedding> embeddings,
        bool ready = true)
    {
        var textSource = Substitute.For<ISearchTextSource>();
        textSource.ListSourcesAsync(Arg.Any<CancellationToken>()).Returns(sources);
        var repository = Substitute.For<ITextEmbeddingRepository>();
        repository.ListAllAsync(Arg.Any<CancellationToken>()).Returns(embeddings);
        var client = Substitute.For<ITextEmbeddingClient>();
        var router = Substitute.For<IProviderRouter>();
        router.GetTextEmbeddingClient(ProviderIds.OpenRouter).Returns(client);
        var index = Substitute.For<ISemanticIndexStatusProvider>();
        index.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(new SemanticIndexStatus(true, embeddings.Count, 0,
            ready ? SemanticIndexState.Ready : SemanticIndexState.Unavailable));
        index.GetStatusSnapshot().Returns(new SemanticIndexStatus(true, embeddings.Count, 0,
            ready ? SemanticIndexState.Ready : SemanticIndexState.Unavailable));
        var service = new SemanticSearchService(textSource, repository, router, index, TimeProvider.System,
            NullLogger<SemanticSearchService>.Instance);
        return (service, client, repository);
    }

    private static TextEmbedding Embedding(string key, Guid sessionId, SearchTextKind kind, string text, float[] vector) =>
        new(key, sessionId, kind,
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))),
            "baai/bge-m3", vector.Length, vector, DateTimeOffset.UtcNow);
}

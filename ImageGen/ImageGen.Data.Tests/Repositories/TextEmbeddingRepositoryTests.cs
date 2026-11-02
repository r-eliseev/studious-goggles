using ImageGen.Core.Domain.Expenses;
using ImageGen.Core.Search;
using ImageGen.Data.Repositories;

namespace ImageGen.Data.Tests.Repositories;

public class TextEmbeddingRepositoryTests
{
    [Fact]
    public async Task UpsertBatchAsync_RoundTripsLittleEndianVectorsAndExpenseAtomically()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var vector = new[] { 0.25f, -0.5f, 1.75f };
        var embedding = new TextEmbedding(
            $"title:{session.Id:D}", session.Id, SearchTextKind.Title, "hash", "baai/bge-m3", vector.Length, vector, TestData.BaseTime);
        var expense = new Expense
        {
            OperationId = "embed:batch-1",
            Kind = ExpenseKind.Embedding,
            ProviderId = "openrouter",
            ModelId = "baai/bge-m3",
            CostUsd = 0.012m,
            OccurredAt = TestData.BaseTime,
            SessionId = session.Id,
        };
        var repository = new TextEmbeddingRepository(test.Database);

        await repository.UpsertBatchAsync([embedding], expense, ct);
        var saved = Assert.Single(await repository.ListAllAsync(ct));

        Assert.Equal(embedding.SourceKey, saved.SourceKey);
        Assert.Equal(embedding.ContentHash, saved.ContentHash);
        Assert.Equal(embedding.Model, saved.Model);
        Assert.Equal(embedding.Dimensions, saved.Dimensions);
        Assert.Equal(vector.Select(BitConverter.SingleToInt32Bits), saved.Vector.Select(BitConverter.SingleToInt32Bits));
        await using var connection = await test.Database.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT vector FROM text_embeddings WHERE source_key = @key;";
        command.Parameters.AddWithValue("@key", embedding.SourceKey);
        Assert.Equal(Convert.FromHexString("0000803E000000BF0000E03F"), (byte[])(await command.ExecuteScalarAsync(ct))!);
        Assert.Equal(0.012m, await new ExpenseRepository(test.Database).SumAsync(ct));
    }

    [Fact]
    public async Task UpsertBatchAsync_ReplacingSourceKeepsOneVectorAndExpense()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var repository = new TextEmbeddingRepository(test.Database);
        var sourceKey = $"title:{session.Id:D}";
        var oldEmbedding = new TextEmbedding(sourceKey, session.Id, SearchTextKind.Title, "old-hash", "baai/bge-m3", 2, [1, 0], TestData.BaseTime);
        var newEmbedding = oldEmbedding with { ContentHash = "new-hash", Vector = [0, 1] };
        var expense = new Expense
        {
            OperationId = "embed:batch-stable",
            Kind = ExpenseKind.Embedding,
            ProviderId = "openrouter",
            ModelId = "baai/bge-m3",
            CostUsd = 0.01m,
            OccurredAt = TestData.BaseTime,
            SessionId = session.Id,
        };

        await repository.UpsertBatchAsync([oldEmbedding], expense, ct);
        await repository.UpsertBatchAsync([newEmbedding], expense, ct);

        var saved = Assert.Single(await repository.ListAllAsync(ct));
        Assert.Equal("new-hash", saved.ContentHash);
        Assert.Equal(new float[] { 0, 1 }, saved.Vector);
        Assert.Equal(1, await new ExpenseRepository(test.Database).CountAsync(ct));
    }

    [Fact]
    public async Task DeleteAsync_SessionDeletionCascadesItsEmbeddings()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        var sessions = new SessionRepository(test.Database);
        await sessions.AddAsync(session, ct);
        var repository = new TextEmbeddingRepository(test.Database);
        await repository.UpsertBatchAsync([
            new TextEmbedding($"title:{session.Id:D}", session.Id, SearchTextKind.Title, "hash", "baai/bge-m3", 1, [1], TestData.BaseTime)
        ], null, ct);

        await sessions.DeleteAsync(session.Id, ct);

        Assert.Empty(await repository.ListAllAsync(ct));
    }

    [Fact]
    public async Task UpsertBatchAsync_InvalidVector_RejectsBeforePersistingAnyExpense()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var embedding = new TextEmbedding($"title:{session.Id:D}", session.Id, SearchTextKind.Title, "hash", "baai/bge-m3", 2, [float.NaN, 0], TestData.BaseTime);
        var expense = new Expense
        {
            OperationId = "embed:invalid-batch",
            Kind = ExpenseKind.Embedding,
            ProviderId = "openrouter",
            ModelId = "baai/bge-m3",
            CostUsd = 0.01m,
            OccurredAt = TestData.BaseTime,
            SessionId = session.Id,
        };

        await Assert.ThrowsAsync<ArgumentException>(() => new TextEmbeddingRepository(test.Database).UpsertBatchAsync([embedding], expense, ct));

        Assert.Equal(0, await new ExpenseRepository(test.Database).CountAsync(ct));
    }
}

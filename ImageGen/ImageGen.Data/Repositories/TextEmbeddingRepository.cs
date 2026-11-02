using System.Buffers.Binary;
using ImageGen.Core.Domain.Expenses;
using ImageGen.Core.Search;
using ImageGen.Core.Storage;
using Microsoft.Data.Sqlite;

namespace ImageGen.Data.Repositories;

public sealed class TextEmbeddingRepository(SqliteDatabase database) : ITextEmbeddingRepository
{
    private const int DeleteBatchSize = 500;

    public async Task<IReadOnlyList<TextEmbedding>> ListAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT source_key, session_id, kind, content_hash, model, dimensions, vector, created_at
            FROM text_embeddings
            ORDER BY source_key;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<TextEmbedding>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var dimensions = SqliteValues.GetInt32(reader, "dimensions");
            var vector = DecodeVector((byte[])reader["vector"], dimensions);
            result.Add(new TextEmbedding(
                SqliteValues.GetString(reader, "source_key"),
                SqliteValues.GetGuid(reader, "session_id"),
                (SearchTextKind)SqliteValues.GetInt32(reader, "kind"),
                SqliteValues.GetString(reader, "content_hash"),
                SqliteValues.GetString(reader, "model"),
                dimensions,
                vector,
                SqliteValues.GetDate(reader, "created_at")));
        }

        return result;
    }

    public async Task UpsertBatchAsync(
        IReadOnlyList<TextEmbedding> embeddings,
        Expense? expense,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(embeddings);
        foreach (var embedding in embeddings)
        {
            Validate(embedding);
        }

        if (embeddings.Count == 0 && expense is null)
        {
            return;
        }

        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var embedding in embeddings)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                INSERT INTO text_embeddings (source_key, session_id, kind, content_hash, model, dimensions, vector, created_at)
                VALUES (@source_key, @session_id, @kind, @content_hash, @model, @dimensions, @vector, @created_at)
                ON CONFLICT(source_key) DO UPDATE SET
                    session_id = excluded.session_id,
                    kind = excluded.kind,
                    content_hash = excluded.content_hash,
                    model = excluded.model,
                    dimensions = excluded.dimensions,
                    vector = excluded.vector,
                    created_at = excluded.created_at;
                """;
            command.Parameters.AddWithValue("@source_key", embedding.SourceKey);
            command.Parameters.AddWithValue("@session_id", SqliteValues.FromGuid(embedding.SessionId));
            command.Parameters.AddWithValue("@kind", (int)embedding.Kind);
            command.Parameters.AddWithValue("@content_hash", embedding.ContentHash);
            command.Parameters.AddWithValue("@model", embedding.Model);
            command.Parameters.AddWithValue("@dimensions", embedding.Dimensions);
            command.Parameters.AddWithValue("@vector", EncodeVector(embedding.Vector));
            command.Parameters.AddWithValue("@created_at", SqliteValues.FromDate(embedding.CreatedAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        if (expense is not null)
        {
            await ExpenseRepository.UpsertAsync(connection, (SqliteTransaction)transaction, expense, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteBySourceKeysAsync(IReadOnlyCollection<string> sourceKeys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceKeys);
        if (sourceKeys.Count == 0)
        {
            return;
        }

        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var batch in sourceKeys.Chunk(DeleteBatchSize))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            var parameters = new string[batch.Length];
            for (var index = 0; index < batch.Length; index++)
            {
                var name = $"@key{index}";
                parameters[index] = name;
                command.Parameters.AddWithValue(name, batch[index]);
            }

            command.CommandText = $"DELETE FROM text_embeddings WHERE source_key IN ({string.Join(", ", parameters)});";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM text_embeddings;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static byte[] EncodeVector(IReadOnlyList<float> vector)
    {
        var bytes = new byte[vector.Count * sizeof(float)];
        for (var index = 0; index < vector.Count; index++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(index * sizeof(float), sizeof(float)), vector[index]);
        }

        return bytes;
    }

    private static float[] DecodeVector(byte[] bytes, int dimensions)
    {
        if (dimensions <= 0 || bytes.Length != dimensions * sizeof(float))
        {
            throw new InvalidDataException("Stored text embedding has an invalid vector length.");
        }

        var vector = new float[dimensions];
        for (var index = 0; index < dimensions; index++)
        {
            vector[index] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(index * sizeof(float), sizeof(float)));
            if (!float.IsFinite(vector[index]))
            {
                throw new InvalidDataException("Stored text embedding contains a non-finite value.");
            }
        }

        return vector;
    }

    private static void Validate(TextEmbedding embedding)
    {
        ArgumentNullException.ThrowIfNull(embedding);
        ArgumentException.ThrowIfNullOrWhiteSpace(embedding.SourceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(embedding.ContentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(embedding.Model);
        ArgumentNullException.ThrowIfNull(embedding.Vector);
        if (embedding.Dimensions <= 0 || embedding.Vector.Length != embedding.Dimensions || embedding.Vector.Any(value => !float.IsFinite(value)))
        {
            throw new ArgumentException("Text embedding dimensions and vector values are invalid.", nameof(embedding));
        }
    }
}

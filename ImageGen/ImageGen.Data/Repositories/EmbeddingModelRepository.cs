using System.Text.Json;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Storage;
using Microsoft.Data.Sqlite;

namespace ImageGen.Data.Repositories;

public sealed class EmbeddingModelRepository(SqliteDatabase database) : IEmbeddingModelRepository
{
    public async Task ReplaceAsync(string providerId, IReadOnlyList<EmbeddingModel> models, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentNullException.ThrowIfNull(models);
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM embedding_models WHERE provider_id = @provider_id;";
            delete.Parameters.AddWithValue("@provider_id", providerId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var source in models)
        {
            var model = source with { ProviderId = providerId };
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO embedding_models (provider_id, id, json, fetched_at)
                VALUES (@provider_id, @id, @json, @fetched_at);
                """;
            insert.Parameters.AddWithValue("@provider_id", providerId);
            insert.Parameters.AddWithValue("@id", model.Id);
            insert.Parameters.AddWithValue("@json", JsonSerializer.Serialize(model));
            insert.Parameters.AddWithValue("@fetched_at", SqliteValues.FromDate(model.FetchedAt));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmbeddingModel>> ListAsync(string providerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT json FROM embedding_models WHERE provider_id = @provider_id ORDER BY json_extract(json, '$.Name'), id;";
        command.Parameters.AddWithValue("@provider_id", providerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var models = new List<EmbeddingModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var model = JsonSerializer.Deserialize<EmbeddingModel>(SqliteValues.GetString(reader, "json"))
                ?? throw new InvalidDataException("A cached embedding model was empty.");
            models.Add(model);
        }

        return models;
    }
}

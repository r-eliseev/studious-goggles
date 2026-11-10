using System.Text.Json;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Storage;
using Microsoft.Data.Sqlite;

namespace ImageGen.Data.Repositories;

public sealed class AudioModelRepository(SqliteDatabase database) : IAudioModelRepository
{
    public async Task ReplaceAsync(string providerId, IReadOnlyList<AudioModel> models, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentNullException.ThrowIfNull(models);
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM audio_models WHERE provider_id = @provider_id;";
            delete.Parameters.AddWithValue("@provider_id", providerId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var model in models)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO audio_models (provider_id, id, json, fetched_at) VALUES (@provider_id, @id, @json, @fetched_at);";
            insert.Parameters.AddWithValue("@provider_id", providerId);
            insert.Parameters.AddWithValue("@id", model.Id);
            insert.Parameters.AddWithValue("@json", JsonSerializer.Serialize(model with { ProviderId = providerId }));
            insert.Parameters.AddWithValue("@fetched_at", SqliteValues.FromDate(model.FetchedAt));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AudioModel>> ListAsync(string providerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT json FROM audio_models WHERE provider_id = @provider_id ORDER BY id;";
        command.Parameters.AddWithValue("@provider_id", providerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var models = new List<AudioModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var model = JsonSerializer.Deserialize<AudioModel>(reader.GetString(0))
                ?? throw new InvalidOperationException("An audio model row contains empty JSON.");
            models.Add(model);
        }

        return models;
    }
}

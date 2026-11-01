using ImageGen.Core.Domain.Descriptions;
using ImageGen.Core.Storage;
using Microsoft.Data.Sqlite;

namespace ImageGen.Data.Repositories;

public sealed class ImageDescriptionRepository(SqliteDatabase database) : IImageDescriptionRepository
{
    private const string Columns = "id, session_id, source_generation_id, mode, provider_id, model_id, text, cost_usd, created_at";

    public async Task AddAsync(ImageDescription description, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(description);
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
            INSERT INTO image_descriptions (id, session_id, source_generation_id, mode, provider_id, model_id, text, cost_usd, created_at)
            VALUES (@id, @session_id, @source_generation_id, @mode, @provider_id, @model_id, @text, @cost_usd, @created_at);
            """;
            command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(description.Id));
            command.Parameters.AddWithValue("@session_id", SqliteValues.FromGuid(description.SessionId));
            command.Parameters.AddWithValue("@source_generation_id", SqliteValues.FromGuid(description.SourceGenerationId));
            command.Parameters.AddWithValue("@mode", (int)description.Mode);
            command.Parameters.AddWithValue("@provider_id", description.ProviderId);
            command.Parameters.AddWithValue("@model_id", description.ModelId);
            command.Parameters.AddWithValue("@text", description.Text);
            command.Parameters.AddWithValue("@cost_usd", SqliteValues.FromNullableDecimal(description.CostUsd));
            command.Parameters.AddWithValue("@created_at", SqliteValues.FromDate(description.CreatedAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        if (description.CostUsd is { } cost)
        {
            await ExpenseRepository.UpsertAsync(connection, transaction, new ImageGen.Core.Domain.Expenses.Expense
            {
                OperationId = $"desc:{description.Id:N}",
                Kind = ImageGen.Core.Domain.Expenses.ExpenseKind.Description,
                ProviderId = description.ProviderId,
                ModelId = description.ModelId,
                CostUsd = cost,
                OccurredAt = description.CreatedAt,
                SessionId = description.SessionId,
            }, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ImageDescription>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM image_descriptions WHERE session_id = @session_id ORDER BY created_at ASC;";
        command.Parameters.AddWithValue("@session_id", SqliteValues.FromGuid(sessionId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<ImageDescription>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ImageDescription
            {
                Id = SqliteValues.GetGuid(reader, "id"),
                SessionId = SqliteValues.GetGuid(reader, "session_id"),
                SourceGenerationId = SqliteValues.GetGuid(reader, "source_generation_id"),
                Mode = (DescriptionMode)SqliteValues.GetInt32(reader, "mode"),
                ProviderId = SqliteValues.GetString(reader, "provider_id"),
                ModelId = SqliteValues.GetString(reader, "model_id"),
                Text = SqliteValues.GetString(reader, "text"),
                CostUsd = SqliteValues.GetNullableDecimal(reader, "cost_usd"),
                CreatedAt = SqliteValues.GetDate(reader, "created_at"),
            });
        }

        return result;
    }
}

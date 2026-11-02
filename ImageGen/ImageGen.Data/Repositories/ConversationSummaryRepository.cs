using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Storage;
using Microsoft.Data.Sqlite;

namespace ImageGen.Data.Repositories;

public sealed class ConversationSummaryRepository(SqliteDatabase database) : IConversationSummaryRepository
{
    private const string Columns = "session_id, text, covers_up_to_message_id, provider_id, model_id, cost_usd, created_at";

    public async Task<ConversationSummary?> GetAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM conversation_summaries WHERE session_id = @session_id;";
        command.Parameters.AddWithValue("@session_id", SqliteValues.FromGuid(sessionId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task UpsertAsync(ConversationSummary summary, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(summary);
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
            INSERT INTO conversation_summaries (session_id, text, covers_up_to_message_id, provider_id, model_id, cost_usd, created_at)
            VALUES (@session_id, @text, @covers_up_to_message_id, @provider_id, @model_id, @cost_usd, @created_at)
            ON CONFLICT(session_id) DO UPDATE SET
                text = excluded.text,
                covers_up_to_message_id = excluded.covers_up_to_message_id,
                provider_id = excluded.provider_id,
                model_id = excluded.model_id,
                cost_usd = excluded.cost_usd,
                created_at = excluded.created_at;
            """;
            command.Parameters.AddWithValue("@session_id", SqliteValues.FromGuid(summary.SessionId));
            command.Parameters.AddWithValue("@text", summary.Text);
            command.Parameters.AddWithValue("@covers_up_to_message_id", SqliteValues.FromGuid(summary.CoversUpToMessageId));
            command.Parameters.AddWithValue("@provider_id", summary.Model.ProviderId);
            command.Parameters.AddWithValue("@model_id", summary.Model.ModelId);
            command.Parameters.AddWithValue("@cost_usd", SqliteValues.FromNullableDecimal(summary.CostUsd));
            command.Parameters.AddWithValue("@created_at", SqliteValues.FromDate(summary.CreatedAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        if (summary.CostUsd is { } cost)
        {
            await ExpenseRepository.UpsertAsync(connection, transaction, new ImageGen.Core.Domain.Expenses.Expense
            {
                OperationId = $"sum:{summary.SessionId:D}:{summary.CoversUpToMessageId:D}",
                Kind = ImageGen.Core.Domain.Expenses.ExpenseKind.Summary,
                ProviderId = summary.Model.ProviderId,
                ModelId = summary.Model.ModelId,
                CostUsd = cost,
                OccurredAt = summary.CreatedAt,
                SessionId = summary.SessionId,
            }, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static ConversationSummary Read(SqliteDataReader reader) => new()
    {
        SessionId = SqliteValues.GetGuid(reader, "session_id"),
        Text = SqliteValues.GetString(reader, "text"),
        CoversUpToMessageId = SqliteValues.GetGuid(reader, "covers_up_to_message_id"),
        Model = new ModelRef(SqliteValues.GetString(reader, "provider_id"), SqliteValues.GetString(reader, "model_id")),
        CostUsd = SqliteValues.GetNullableDecimal(reader, "cost_usd"),
        CreatedAt = SqliteValues.GetDate(reader, "created_at"),
    };
}

using ImageGen.Core.Domain.Expenses;
using ImageGen.Core.Storage;
using Microsoft.Data.Sqlite;

namespace ImageGen.Data.Repositories;

public sealed class ExpenseRepository(SqliteDatabase database) : IExpenseRepository
{
    public async Task<IReadOnlyList<Expense>> ListAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT operation_id, kind, provider_id, model_id, cost_usd, occurred_at, session_id
            FROM expenses
            WHERE occurred_at >= @from AND occurred_at < @to
            ORDER BY occurred_at, operation_id;
            """;
        command.Parameters.AddWithValue("@from", SqliteValues.FromDate(from));
        command.Parameters.AddWithValue("@to", SqliteValues.FromDate(to));
        var result = new List<Expense>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new Expense
            {
                OperationId = SqliteValues.GetString(reader, "operation_id"),
                Kind = (ExpenseKind)SqliteValues.GetInt32(reader, "kind"),
                ProviderId = SqliteValues.GetString(reader, "provider_id"),
                ModelId = SqliteValues.GetString(reader, "model_id"),
                CostUsd = SqliteValues.GetDecimal(reader, "cost_usd"),
                OccurredAt = SqliteValues.GetDate(reader, "occurred_at"),
                SessionId = SqliteValues.GetNullableGuid(reader, "session_id"),
            });
        }

        return result;
    }

    internal static async Task UpsertAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Expense expense,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO expenses (operation_id, kind, provider_id, model_id, cost_usd, occurred_at, session_id)
            VALUES (@operation_id, @kind, @provider_id, @model_id, @cost_usd, @occurred_at, @session_id)
            ON CONFLICT(operation_id) DO UPDATE SET
                kind = excluded.kind,
                provider_id = excluded.provider_id,
                model_id = excluded.model_id,
                cost_usd = excluded.cost_usd,
                occurred_at = excluded.occurred_at,
                session_id = excluded.session_id;
            """;
        command.Parameters.AddWithValue("@operation_id", expense.OperationId);
        command.Parameters.AddWithValue("@kind", (int)expense.Kind);
        command.Parameters.AddWithValue("@provider_id", expense.ProviderId);
        command.Parameters.AddWithValue("@model_id", expense.ModelId);
        command.Parameters.AddWithValue("@cost_usd", SqliteValues.FromDecimal(expense.CostUsd));
        command.Parameters.AddWithValue("@occurred_at", SqliteValues.FromDate(expense.OccurredAt));
        command.Parameters.AddWithValue("@session_id", SqliteValues.FromNullableGuid(expense.SessionId));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<decimal> SumAsync(CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(SUM(CAST(cost_usd AS NUMERIC)), 0) FROM expenses;";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToDecimal(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM expenses;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }
}

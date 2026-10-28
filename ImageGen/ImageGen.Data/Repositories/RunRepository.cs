using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Storage;
using Microsoft.Data.Sqlite;

namespace ImageGen.Data.Repositories;

public sealed class RunRepository(SqliteDatabase database) : IRunRepository
{
    private const string GenerationColumns = """
        g.id, g.run_id, g.session_id, g.provider_id, g.model_id, g.prompt, g.seed, g.seed_applied, g.status,
        g.image_path, g.media_type, g.cost_usd, g.error_kind, g.error_message, g.created_at, g.completed_at, g.sort_order
        """;

    public async Task AddRunAsync(Run run, IReadOnlyList<Generation> generations, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(generations);
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO runs (id, session_id, prompt, origin, created_at)
                VALUES (@id, @session_id, @prompt, @origin, @created_at);
                """;
            command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(run.Id));
            command.Parameters.AddWithValue("@session_id", SqliteValues.FromGuid(run.SessionId));
            command.Parameters.AddWithValue("@prompt", run.Prompt);
            command.Parameters.AddWithValue("@origin", (int)run.Origin);
            command.Parameters.AddWithValue("@created_at", SqliteValues.FromDate(run.CreatedAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var generation in generations)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO generations (id, run_id, session_id, provider_id, model_id, prompt, seed, seed_applied, status,
                    image_path, media_type, cost_usd, error_kind, error_message, created_at, completed_at, sort_order)
                VALUES (@id, @run_id, @session_id, @provider_id, @model_id, @prompt, @seed, @seed_applied, @status,
                    @image_path, @media_type, @cost_usd, @error_kind, @error_message, @created_at, @completed_at, @sort_order);
                """;
            AddGenerationParameters(command, generation);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Run?> GetRunAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, session_id, prompt, origin, created_at FROM runs WHERE id = @id;";
        command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(id));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRun(reader) : null;
    }

    public async Task<IReadOnlyList<Run>> ListRunsBySessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, session_id, prompt, origin, created_at
            FROM runs
            WHERE session_id = @session_id
            ORDER BY created_at ASC;
            """;
        command.Parameters.AddWithValue("@session_id", SqliteValues.FromGuid(sessionId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<Run>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadRun(reader));
        }

        return result;
    }

    public async Task<Generation?> GetGenerationAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {GenerationColumns} FROM generations g WHERE g.id = @id;";
        command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(id));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadGeneration(reader) : null;
    }

    public async Task<IReadOnlyList<Generation>> ListGenerationsBySessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {GenerationColumns}
            FROM generations g
            JOIN runs r ON r.id = g.run_id
            WHERE g.session_id = @session_id
            ORDER BY r.created_at ASC, g.sort_order ASC;
            """;
        command.Parameters.AddWithValue("@session_id", SqliteValues.FromGuid(sessionId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<Generation>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadGeneration(reader));
        }

        return result;
    }

    public async Task UpdateGenerationAsync(Generation generation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(generation);
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE generations
            SET run_id = @run_id, session_id = @session_id, provider_id = @provider_id, model_id = @model_id, prompt = @prompt, seed = @seed,
                seed_applied = @seed_applied, status = @status, image_path = @image_path, media_type = @media_type,
                cost_usd = @cost_usd, error_kind = @error_kind, error_message = @error_message,
                created_at = @created_at, completed_at = @completed_at, sort_order = @sort_order
            WHERE id = @id;
            """;
        AddGenerationParameters(command, generation);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> MarkUnfinishedAsInterruptedAsync(DateTimeOffset completedAt, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE generations
            SET status = @failed, error_kind = @interrupted, error_message = @message, completed_at = @completed_at
            WHERE status IN (@queued, @running);
            """;
        command.Parameters.AddWithValue("@failed", (int)GenerationStatus.Failed);
        command.Parameters.AddWithValue("@interrupted", (int)ErrorKind.Interrupted);
        command.Parameters.AddWithValue("@message", "The app was closed before the generation finished.");
        command.Parameters.AddWithValue("@completed_at", SqliteValues.FromDate(completedAt));
        command.Parameters.AddWithValue("@queued", (int)GenerationStatus.Queued);
        command.Parameters.AddWithValue("@running", (int)GenerationStatus.Running);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddGenerationParameters(SqliteCommand command, Generation generation)
    {
        command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(generation.Id));
        command.Parameters.AddWithValue("@run_id", SqliteValues.FromGuid(generation.RunId));
        command.Parameters.AddWithValue("@session_id", SqliteValues.FromGuid(generation.SessionId));
        command.Parameters.AddWithValue("@provider_id", generation.ProviderId);
        command.Parameters.AddWithValue("@model_id", generation.ModelId);
        command.Parameters.AddWithValue("@prompt", generation.Prompt);
        command.Parameters.AddWithValue("@seed", generation.Seed);
        command.Parameters.AddWithValue("@seed_applied", SqliteValues.FromBool(generation.SeedApplied));
        command.Parameters.AddWithValue("@status", (int)generation.Status);
        command.Parameters.AddWithValue("@image_path", SqliteValues.FromNullableString(generation.ImagePath));
        command.Parameters.AddWithValue("@media_type", SqliteValues.FromNullableString(generation.MediaType));
        command.Parameters.AddWithValue("@cost_usd", SqliteValues.FromNullableDecimal(generation.CostUsd));
        command.Parameters.AddWithValue(
            "@error_kind",
            generation.ErrorKind.HasValue ? (int)generation.ErrorKind.Value : DBNull.Value);
        command.Parameters.AddWithValue("@error_message", SqliteValues.FromNullableString(generation.ErrorMessage));
        command.Parameters.AddWithValue("@created_at", SqliteValues.FromDate(generation.CreatedAt));
        command.Parameters.AddWithValue("@completed_at", SqliteValues.FromNullableDate(generation.CompletedAt));
        command.Parameters.AddWithValue("@sort_order", generation.SortOrder);
    }

    private static Run ReadRun(SqliteDataReader reader) => new()
    {
        Id = SqliteValues.GetGuid(reader, "id"),
        SessionId = SqliteValues.GetGuid(reader, "session_id"),
        Prompt = SqliteValues.GetString(reader, "prompt"),
        Origin = (RunOrigin)SqliteValues.GetInt32(reader, "origin"),
        CreatedAt = SqliteValues.GetDate(reader, "created_at"),
    };

    private static Generation ReadGeneration(SqliteDataReader reader)
    {
        var errorKind = SqliteValues.GetNullableInt32(reader, "error_kind");
        return new Generation
        {
            Id = SqliteValues.GetGuid(reader, "id"),
            RunId = SqliteValues.GetGuid(reader, "run_id"),
            SessionId = SqliteValues.GetGuid(reader, "session_id"),
            ProviderId = SqliteValues.GetString(reader, "provider_id"),
            ModelId = SqliteValues.GetString(reader, "model_id"),
            Prompt = SqliteValues.GetString(reader, "prompt"),
            Seed = SqliteValues.GetInt64(reader, "seed"),
            SeedApplied = SqliteValues.GetBool(reader, "seed_applied"),
            Status = (GenerationStatus)SqliteValues.GetInt32(reader, "status"),
            ImagePath = SqliteValues.GetNullableString(reader, "image_path"),
            MediaType = SqliteValues.GetNullableString(reader, "media_type"),
            CostUsd = SqliteValues.GetNullableDecimal(reader, "cost_usd"),
            ErrorKind = errorKind.HasValue ? (ErrorKind)errorKind.Value : null,
            ErrorMessage = SqliteValues.GetNullableString(reader, "error_message"),
            CreatedAt = SqliteValues.GetDate(reader, "created_at"),
            CompletedAt = SqliteValues.GetNullableDate(reader, "completed_at"),
            SortOrder = SqliteValues.GetInt32(reader, "sort_order"),
        };
    }
}

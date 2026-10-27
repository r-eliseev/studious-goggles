using System.Text.Json;
using ImageGen.Core.Domain;
using ImageGen.Core.Serialization;
using ImageGen.Core.Storage;
using Microsoft.Data.Sqlite;

namespace ImageGen.Data.Repositories;

public sealed class SessionRepository(SqliteDatabase database) : ISessionRepository
{
    private const string SelectColumns = "SELECT id, title, created_at, updated_at, settings_json FROM sessions";

    public async Task AddAsync(Session session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO sessions (id, title, created_at, updated_at, settings_json)
            VALUES (@id, @title, @created_at, @updated_at, @settings_json);
            """;
        AddParameters(command, session);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Session session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE sessions
            SET title = @title, created_at = @created_at, updated_at = @updated_at, settings_json = @settings_json
            WHERE id = @id;
            """;
        AddParameters(command, session);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Session?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectColumns + " WHERE id = @id;";
        command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(id));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task<IReadOnlyList<Session>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectColumns + " ORDER BY updated_at DESC;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<Session>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(Read(reader));
        }

        return result;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM sessions WHERE id = @id;";
        command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(id));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(SqliteCommand command, Session session)
    {
        command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(session.Id));
        command.Parameters.AddWithValue("@title", session.Title);
        command.Parameters.AddWithValue("@created_at", SqliteValues.FromDate(session.CreatedAt));
        command.Parameters.AddWithValue("@updated_at", SqliteValues.FromDate(session.UpdatedAt));
        command.Parameters.AddWithValue(
            "@settings_json",
            JsonSerializer.Serialize(session.Settings, CoreJsonContext.Default.SessionSettings));
    }

    private static Session Read(SqliteDataReader reader) => new()
    {
        Id = SqliteValues.GetGuid(reader, "id"),
        Title = SqliteValues.GetString(reader, "title"),
        CreatedAt = SqliteValues.GetDate(reader, "created_at"),
        UpdatedAt = SqliteValues.GetDate(reader, "updated_at"),
        Settings = JsonSerializer.Deserialize(
                SqliteValues.GetString(reader, "settings_json"),
                CoreJsonContext.Default.SessionSettings)
            ?? SessionSettings.CreateDefault(),
    };
}

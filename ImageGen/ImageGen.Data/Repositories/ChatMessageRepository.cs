using ImageGen.Core.Domain;
using ImageGen.Core.Storage;
using Microsoft.Data.Sqlite;

namespace ImageGen.Data.Repositories;

public sealed class ChatMessageRepository(SqliteDatabase database) : IChatMessageRepository
{
    public async Task AddAsync(ChatMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO chat_messages (id, session_id, role, text, created_at, run_id, is_error)
            VALUES (@id, @session_id, @role, @text, @created_at, @run_id, @is_error);
            """;
        command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(message.Id));
        command.Parameters.AddWithValue("@session_id", SqliteValues.FromGuid(message.SessionId));
        command.Parameters.AddWithValue("@role", (int)message.Role);
        command.Parameters.AddWithValue("@text", message.Text);
        command.Parameters.AddWithValue("@created_at", SqliteValues.FromDate(message.CreatedAt));
        command.Parameters.AddWithValue("@run_id", SqliteValues.FromNullableGuid(message.RunId));
        command.Parameters.AddWithValue("@is_error", SqliteValues.FromBool(message.IsError));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, session_id, role, text, created_at, run_id, is_error
            FROM chat_messages
            WHERE session_id = @session_id
            ORDER BY created_at ASC;
            """;
        command.Parameters.AddWithValue("@session_id", SqliteValues.FromGuid(sessionId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<ChatMessage>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(Read(reader));
        }

        return result;
    }

    private static ChatMessage Read(SqliteDataReader reader) => new()
    {
        Id = SqliteValues.GetGuid(reader, "id"),
        SessionId = SqliteValues.GetGuid(reader, "session_id"),
        Role = (ChatRole)SqliteValues.GetInt32(reader, "role"),
        Text = SqliteValues.GetString(reader, "text"),
        CreatedAt = SqliteValues.GetDate(reader, "created_at"),
        RunId = SqliteValues.GetNullableGuid(reader, "run_id"),
        IsError = SqliteValues.GetBool(reader, "is_error"),
    };
}

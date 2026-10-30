using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Storage;
using Microsoft.Data.Sqlite;

namespace ImageGen.Data.Repositories;

public sealed class ChatMessageRepository(SqliteDatabase database) : IChatMessageRepository
{
    public async Task AddUserWithAttachmentsAsync(
        ChatMessage message,
        IReadOnlyList<Guid> attachmentIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(attachmentIds);
        if (message.Role != ChatRole.User)
        {
            throw new ArgumentException("Only user messages can be accepted with attachments.", nameof(message));
        }

        if (attachmentIds.Count != attachmentIds.Distinct().Count())
        {
            throw new ArgumentException("Attachment ids must be unique.", nameof(attachmentIds));
        }

        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO chat_messages (id, session_id, role, text, created_at, run_id, is_error, error_kind)
                VALUES (@id, @session_id, @role, @text, @created_at, @run_id, @is_error, @error_kind);
                """;
            BindMessage(command, message);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var attachmentId in attachmentIds)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE chat_attachments
                SET message_id = @message_id
                WHERE id = @id AND session_id = @session_id AND message_id IS NULL;
                """;
            command.Parameters.AddWithValue("@message_id", SqliteValues.FromGuid(message.Id));
            command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(attachmentId));
            command.Parameters.AddWithValue("@session_id", SqliteValues.FromGuid(message.SessionId));
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new InvalidOperationException("An attachment is missing, belongs to another session, or is already linked.");
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AddAsync(ChatMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO chat_messages (id, session_id, role, text, created_at, run_id, is_error, error_kind)
            VALUES (@id, @session_id, @role, @text, @created_at, @run_id, @is_error, @error_kind);
            """;
        BindMessage(command, message);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void BindMessage(SqliteCommand command, ChatMessage message)
    {
        command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(message.Id));
        command.Parameters.AddWithValue("@session_id", SqliteValues.FromGuid(message.SessionId));
        command.Parameters.AddWithValue("@role", (int)message.Role);
        command.Parameters.AddWithValue("@text", message.Text);
        command.Parameters.AddWithValue("@created_at", SqliteValues.FromDate(message.CreatedAt));
        command.Parameters.AddWithValue("@run_id", SqliteValues.FromNullableGuid(message.RunId));
        command.Parameters.AddWithValue("@is_error", SqliteValues.FromBool(message.IsError));
        command.Parameters.AddWithValue("@error_kind", message.ErrorKind is { } kind ? (object)(int)kind : DBNull.Value);
    }

    public async Task<IReadOnlyList<ChatMessage>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, session_id, role, text, created_at, run_id, is_error, error_kind
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
        ErrorKind = SqliteValues.GetNullableInt32(reader, "error_kind") is { } kind ? (ErrorKind)kind : null,
    };
}

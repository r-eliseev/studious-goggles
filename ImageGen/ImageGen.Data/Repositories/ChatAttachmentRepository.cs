using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Storage;
using Microsoft.Data.Sqlite;

namespace ImageGen.Data.Repositories;

public sealed class ChatAttachmentRepository(SqliteDatabase database) : IChatAttachmentRepository
{
    private const string Columns = "id, session_id, message_id, relative_path, media_type, created_at";

    public async Task AddAsync(ChatAttachment attachment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO chat_attachments (id, session_id, message_id, relative_path, media_type, created_at)
            VALUES (@id, @session_id, @message_id, @relative_path, @media_type, @created_at);
            """;
        command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(attachment.Id));
        command.Parameters.AddWithValue("@session_id", SqliteValues.FromGuid(attachment.SessionId));
        command.Parameters.AddWithValue("@message_id", SqliteValues.FromNullableGuid(attachment.MessageId));
        command.Parameters.AddWithValue("@relative_path", attachment.RelativePath);
        command.Parameters.AddWithValue("@media_type", attachment.MediaType);
        command.Parameters.AddWithValue("@created_at", SqliteValues.FromDate(attachment.CreatedAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ChatAttachment?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM chat_attachments WHERE id = @id;";
        command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(id));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task LinkToMessageAsync(IReadOnlyList<Guid> attachmentIds, Guid messageId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attachmentIds);
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        foreach (var attachmentId in attachmentIds)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE chat_attachments SET message_id = @message_id WHERE id = @id;";
            command.Parameters.AddWithValue("@message_id", SqliteValues.FromGuid(messageId));
            command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(attachmentId));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatAttachment>> ListByMessageAsync(Guid messageId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM chat_attachments WHERE message_id = @message_id ORDER BY created_at ASC;";
        command.Parameters.AddWithValue("@message_id", SqliteValues.FromGuid(messageId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<ChatAttachment>();
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
        command.CommandText = "DELETE FROM chat_attachments WHERE id = @id;";
        command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(id));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static ChatAttachment Read(SqliteDataReader reader) => new()
    {
        Id = SqliteValues.GetGuid(reader, "id"),
        SessionId = SqliteValues.GetGuid(reader, "session_id"),
        MessageId = SqliteValues.GetNullableGuid(reader, "message_id"),
        RelativePath = SqliteValues.GetString(reader, "relative_path"),
        MediaType = SqliteValues.GetString(reader, "media_type"),
        CreatedAt = SqliteValues.GetDate(reader, "created_at"),
    };
}

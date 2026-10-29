using ImageGen.Core.Search;
using Microsoft.Data.Sqlite;

namespace ImageGen.Data.Repositories;

/// <summary>Reads the titles, prompts and picture descriptions of all sessions in one query. Column kinds: 0 title, 1 prompt, 2 description.</summary>
public sealed class SearchTextSource(SqliteDatabase database) : ISearchTextSource
{
    private const string Query = """
        SELECT id AS session_id, 0 AS kind, title AS text FROM sessions
        UNION ALL
        SELECT DISTINCT session_id, 1, prompt FROM runs
        UNION ALL
        SELECT DISTINCT session_id, 1, prompt FROM generations
        UNION ALL
        SELECT session_id, 2, text FROM image_descriptions;
        """;

    public async Task<IReadOnlyList<SearchText>> ListAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = Query;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<SearchText>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var kind = (SearchTextKind)SqliteValues.GetInt32(reader, "kind");
            result.Add(new SearchText(SqliteValues.GetGuid(reader, "session_id"), kind, SqliteValues.GetString(reader, "text")));
        }

        return result;
    }
}

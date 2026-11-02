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
    private const string SourcesQuery = """
        SELECT source_key, session_id, kind, text
        FROM (
            SELECT 'title:' || id AS source_key, id AS session_id, 0 AS kind, title AS text, updated_at FROM sessions
            UNION ALL
            SELECT 'prompt:' || runs.id, runs.session_id, 1, runs.prompt, sessions.updated_at
            FROM runs INNER JOIN sessions ON sessions.id = runs.session_id
            UNION ALL
            SELECT 'desc:' || image_descriptions.id, image_descriptions.session_id, 2, image_descriptions.text, sessions.updated_at
            FROM image_descriptions INNER JOIN sessions ON sessions.id = image_descriptions.session_id
        )
        ORDER BY updated_at DESC, source_key
        LIMIT 10001;
        """;
    private const string CountSourcesQuery = """
        SELECT COUNT(*)
        FROM (
            SELECT id FROM sessions
            UNION ALL SELECT id FROM runs
            UNION ALL SELECT id FROM image_descriptions
        );
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

    public async Task<IReadOnlyList<SearchSource>> ListSourcesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SourcesQuery;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<SearchSource>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new SearchSource(
                SqliteValues.GetString(reader, "source_key"),
                SqliteValues.GetGuid(reader, "session_id"),
                (SearchTextKind)SqliteValues.GetInt32(reader, "kind"),
                SqliteValues.GetString(reader, "text")));
        }

        return result;
    }

    public async Task<int> CountSourcesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = CountSourcesQuery;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }
}

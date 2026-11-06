using ImageGen.Core.Domain.Gallery;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Storage;
using Microsoft.Data.Sqlite;

namespace ImageGen.Data.Repositories;

public sealed class GalleryRepository(SqliteDatabase database) : IGalleryRepository
{
    public async Task<GalleryPage> ListAsync(GalleryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(query.PageSize, 100);
        if (query.From is { } from && query.To is { } to && from >= to)
        {
            return new GalleryPage([], null);
        }

        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var conditions = new List<string> { "g.kind = @image", "g.status = @succeeded", "g.image_path IS NOT NULL", "g.completed_at IS NOT NULL" };
        command.Parameters.AddWithValue("@image", (int)MediaKind.Image);
        command.Parameters.AddWithValue("@succeeded", (int)GenerationStatus.Succeeded);
        if (query.Cursor is { } cursor)
        {
            conditions.Add("(g.completed_at, g.id) < (@cursor_at, @cursor_id)");
            command.Parameters.AddWithValue("@cursor_at", SqliteValues.FromDate(cursor.CompletedAt));
            command.Parameters.AddWithValue("@cursor_id", SqliteValues.FromGuid(cursor.GenerationId));
        }
        if (query.Model is { } model)
        {
            conditions.Add("g.provider_id = @provider_id AND g.model_id = @model_id");
            command.Parameters.AddWithValue("@provider_id", model.ProviderId);
            command.Parameters.AddWithValue("@model_id", model.ModelId);
        }
        if (query.From is { } start)
        {
            conditions.Add("g.completed_at >= @from");
            command.Parameters.AddWithValue("@from", SqliteValues.FromDate(start));
        }
        if (query.To is { } end)
        {
            conditions.Add("g.completed_at < @to");
            command.Parameters.AddWithValue("@to", SqliteValues.FromDate(end));
        }

        command.CommandText = $"""
            SELECT g.id, g.run_id, g.session_id, s.title AS session_title, g.provider_id, g.model_id,
                   g.prompt, g.image_path, g.media_type, g.cost_usd, g.completed_at
            FROM generations g
            JOIN sessions s ON s.id = g.session_id
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY g.completed_at DESC, g.id DESC
            LIMIT @limit;
            """;
        command.Parameters.AddWithValue("@limit", query.PageSize + 1);
        var items = new List<GalleryItem>(query.PageSize + 1);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new GalleryItem(
                    SqliteValues.GetGuid(reader, "id"), SqliteValues.GetGuid(reader, "run_id"),
                    SqliteValues.GetGuid(reader, "session_id"), SqliteValues.GetString(reader, "session_title"),
                    new ModelRef(SqliteValues.GetString(reader, "provider_id"), SqliteValues.GetString(reader, "model_id")),
                    SqliteValues.GetString(reader, "prompt"), SqliteValues.GetString(reader, "image_path"),
                    SqliteValues.GetNullableString(reader, "media_type"), SqliteValues.GetNullableDecimal(reader, "cost_usd"),
                    SqliteValues.GetDate(reader, "completed_at")));
            }
        }

        var hasMore = items.Count > query.PageSize;
        if (hasMore) items.RemoveAt(items.Count - 1);
        var last = hasMore ? items[^1] : null;
        return new GalleryPage(items, last is null ? null : new GalleryCursor(last.CompletedAt, last.GenerationId));
    }

    public async Task<IReadOnlyList<ModelRef>> ListModelsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT provider_id, model_id FROM generations
            WHERE kind = @image AND status = @succeeded AND image_path IS NOT NULL AND completed_at IS NOT NULL
            ORDER BY provider_id, model_id;
            """;
        command.Parameters.AddWithValue("@image", (int)MediaKind.Image);
        command.Parameters.AddWithValue("@succeeded", (int)GenerationStatus.Succeeded);
        var models = new List<ModelRef>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            models.Add(new ModelRef(SqliteValues.GetString(reader, "provider_id"), SqliteValues.GetString(reader, "model_id")));
        }
        return models;
    }
}

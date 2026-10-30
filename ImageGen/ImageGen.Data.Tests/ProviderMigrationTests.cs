using ImageGen.Data.Repositories;
using Microsoft.Data.Sqlite;

namespace ImageGen.Data.Tests;

/// <summary>Upgrades a database that was created by schema version 1 (before provider ids) and keeps its data.</summary>
public class ProviderMigrationTests
{
    [Fact]
    public async Task InitializeAsync_FromVersionOne_KeepsRowsAndAssignsOpenRouter()
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "imagegen-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, SqliteDatabase.FileName);
        try
        {
            await CreateVersionOneDatabaseAsync(path, ct);

            await InitializeThroughVersionAsync(path, 2, ct);

            await using var connection = new SqliteConnection($"Data Source={path}");
            await connection.OpenAsync(ct);
            Assert.Equal("openrouter", await ScalarAsync(connection, "SELECT provider_id FROM image_models WHERE id = 'vendor/old';", ct));
            Assert.Equal("openrouter", await ScalarAsync(connection, "SELECT provider_id FROM generations WHERE id = 'g1';", ct));
            Assert.Equal("vendor/old", await ScalarAsync(connection, "SELECT model_id FROM generations WHERE id = 'g1';", ct));
            Assert.Contains("\"a/b\"", (string?)await ScalarAsync(connection, "SELECT settings_json FROM sessions WHERE id = 's1';", ct));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task InitializeThroughVersionAsync(string path, int version, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync(cancellationToken);
        foreach (var migration in Migrations.All.Where(migration => migration.Number > 1 && migration.Number <= version))
        {
            await using var apply = connection.CreateCommand();
            apply.CommandText = migration.Script;
            await apply.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var updateVersion = connection.CreateCommand();
        updateVersion.CommandText = $"PRAGMA user_version = {version};";
        await updateVersion.ExecuteNonQueryAsync(cancellationToken);
    }

    [Fact]
    public async Task InitializeAsync_AfterUpgrade_SameModelIdUnderTwoProvidersIsAllowed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        await using var connection = await test.Database.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO image_models (provider_id, id, name, supports_seed, supports_image_input, max_images_per_request, fetched_at)
            VALUES ('openrouter', 'same/id', 'A', 0, 0, 1, '2026-10-28T00:00:00Z'),
                   ('local', 'same/id', 'B', 0, 0, 1, '2026-10-28T00:00:00Z');
            """;
        await command.ExecuteNonQueryAsync(ct);

        command.CommandText = "SELECT COUNT(*) FROM image_models WHERE id = 'same/id';";
        Assert.Equal(2L, await command.ExecuteScalarAsync(ct));
    }

    private static async Task CreateVersionOneDatabaseAsync(string path, CancellationToken ct)
    {
        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync(ct);
        await using (var schema = connection.CreateCommand())
        {
            schema.CommandText = Migrations.All[0].Script;
            await schema.ExecuteNonQueryAsync(ct);
        }

        await using (var data = connection.CreateCommand())
        {
            data.CommandText = """
                INSERT INTO sessions (id, title, created_at, updated_at, settings_json)
                VALUES ('s1', 'Old', '2026-10-27T10:00:00Z', '2026-10-27T10:00:00Z',
                        '{"imageModelIds":["a/b"],"runsPerModel":1,"assistantModelId":"google/gemma-4-31b-it:free"}');
                INSERT INTO runs (id, session_id, prompt, origin, created_at)
                VALUES ('r1', 's1', 'fox', 0, '2026-10-27T10:00:00Z');
                INSERT INTO generations (id, run_id, session_id, model_id, prompt, seed, seed_applied, status,
                    created_at, sort_order)
                VALUES ('g1', 'r1', 's1', 'vendor/old', 'fox', 1, 1, 2, '2026-10-27T10:00:00Z', 0);
                INSERT INTO image_models (id, name, supports_seed, supports_image_input, max_images_per_request, fetched_at)
                VALUES ('vendor/old', 'Old', 1, 0, 1, '2026-10-27T10:00:00Z');
                PRAGMA user_version = 1;
                """;
            await data.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(ct);
    }
}

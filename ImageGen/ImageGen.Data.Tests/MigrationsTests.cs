using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;

namespace ImageGen.Data.Tests;

public class MigrationsTests
{
    [Fact]
    public void Load_UnorderedFiles_SortsByNumber()
    {
        var migrations = Migrations.Load(
        [
            ("0002_b.sql", "B"),
            ("0001_a.sql", "A"),
        ]);

        Assert.Equal(["0001_a.sql", "0002_b.sql"], migrations.Select(m => m.Name));
        Assert.Equal([1, 2], migrations.Select(m => m.Number));
    }

    [Fact]
    public void Load_NumberGap_ThrowsNamingMissingNumber()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Migrations.Load(
        [
            ("0001_a.sql", "A"),
            ("0003_c.sql", "C"),
        ]));

        Assert.Contains("0002", exception.Message);
    }

    [Fact]
    public void Load_DuplicateNumber_ThrowsNamingBothFiles()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Migrations.Load(
        [
            ("0001_a.sql", "A"),
            ("0001_b.sql", "B"),
        ]));

        Assert.Contains("0001_a.sql", exception.Message);
        Assert.Contains("0001_b.sql", exception.Message);
    }

    [Fact]
    public void Load_NameWithoutFourDigits_ThrowsNamingFile()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Migrations.Load(
        [
            ("1_a.sql", "A"),
        ]));

        Assert.Contains("1_a.sql", exception.Message);
    }

    [Fact]
    public void Load_NameWithUpperCaseOrDash_ThrowsInvalidName()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Migrations.Load(
        [
            ("0001-A.sql", "A"),
        ]));

        Assert.Contains("0001-A.sql", exception.Message);
    }

    [Fact]
    public void Load_NumberZero_ThrowsNamingFile()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Migrations.Load(
        [
            ("0000_a.sql", "A"),
        ]));

        Assert.Contains("0000_a.sql", exception.Message);
    }

    [Fact]
    public void Load_EmptySet_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Migrations.Load([]));
    }

    [Fact]
    public void Load_Script_IsReturnedUnchanged()
    {
        const string script = "CREATE TABLE t (id INTEGER);\n-- комментарий\r\n";

        var migrations = Migrations.Load([("0001_a.sql", script)]);

        Assert.Equal(script, migrations[0].Script);
    }

    [Fact]
    public void All_EmbeddedScripts_ContainInitialWithNumberOne()
    {
        var initial = Assert.Single(Migrations.All, m => m.Name == "0001_initial.sql");

        Assert.Equal(1, initial.Number);
    }

    [Fact]
    public void All_Count_EqualsNumberOfSqlFilesInProject()
    {
        var migrationsFolder = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(ThisFilePath())!, "..", "ImageGen.Data", "Migrations"));

        var sqlFiles = Directory.GetFiles(migrationsFolder, "*.sql");

        Assert.Equal(sqlFiles.Length, Migrations.All.Count);
    }

    [Fact]
    public async Task All_ImageModelPricesMigration_AddsColumnAndClearsCache()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "imagegen-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, SqliteDatabase.FileName);
        try
        {
            await using (var connection = new SqliteConnection($"Data Source={path};Foreign Keys=True"))
            {
                await connection.OpenAsync(cancellationToken);
                foreach (var migration in Migrations.All.Where(migration => migration.Number <= 8))
                {
                    await using var apply = connection.CreateCommand();
                    apply.CommandText = migration.Script;
                    await apply.ExecuteNonQueryAsync(cancellationToken);
                }

                await using (var seed = connection.CreateCommand())
                {
                    seed.CommandText = """
                        INSERT INTO sessions (id, title, created_at, updated_at, settings_json)
                        VALUES ('s1', 'Keep', '2026-10-29T00:00:00Z', '2026-10-29T00:00:00Z', '{}');
                        INSERT INTO runs (id, session_id, prompt, origin, created_at)
                        VALUES ('r1', 's1', 'keep this run', 0, '2026-10-29T00:00:00Z');
                        INSERT INTO image_models (provider_id, id, name, supports_seed, supports_image_input,
                            max_images_per_request, fetched_at, supported_parameters_json)
                        VALUES ('openrouter', 'vendor/model', 'Cached model', 1, 0, 1,
                            '2026-10-29T00:00:00Z', '[]');
                        PRAGMA user_version = 8;
                        """;
                    await seed.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await new SqliteDatabase(path).InitializeAsync(cancellationToken);

            await using var check = new SqliteConnection($"Data Source={path};Foreign Keys=True");
            await check.OpenAsync(cancellationToken);
            await using var command = check.CreateCommand();
            command.CommandText = "PRAGMA table_info(image_models);";
            await using var columns = await command.ExecuteReaderAsync(cancellationToken);
            var hasPricesColumn = false;
            while (await columns.ReadAsync(cancellationToken))
            {
                if (columns.GetString(1) == "prices_json")
                {
                    hasPricesColumn = true;
                }
            }

            Assert.True(hasPricesColumn);
            Assert.Equal(0L, await ScalarAsync(check, "SELECT COUNT(*) FROM image_models;", cancellationToken));
            Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM sessions WHERE id = 's1';", cancellationToken));
            Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM runs WHERE id = 'r1';", cancellationToken));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static string ThisFilePath([CallerFilePath] string path = "") => path;
}

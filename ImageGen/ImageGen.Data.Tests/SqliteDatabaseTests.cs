using ImageGen.Data.Tests.TestDoubles;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace ImageGen.Data.Tests;

public class SqliteDatabaseTests
{
    [Fact]
    public async Task InitializeAsync_NewFile_LogsSchemaVersionChangeFromZero()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "imagegen-tests", Guid.NewGuid().ToString("N"));
        var logger = new CapturingLogger<SqliteDatabase>();
        var database = new SqliteDatabase(Path.Combine(directory, SqliteDatabase.FileName), logger);
        try
        {
            await database.InitializeAsync(cancellationToken);

            var ready = Assert.Single(logger.Entries, e => e.Message.StartsWith("Database ready", StringComparison.Ordinal));
            Assert.Equal(LogLevel.Information, ready.Level);
            Assert.Contains($"schema version {Migrations.All.Count} (was 0)", ready.Message, StringComparison.Ordinal);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task InitializeAsync_NewFile_SetsSchemaVersionToMigrationCount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var testDatabase = await TestDatabase.CreateAsync(cancellationToken);

        await using var connection = await testDatabase.Database.OpenAsync(cancellationToken);
        var version = await SqliteDatabase.GetSchemaVersionAsync(connection, cancellationToken);

        Assert.Equal(Migrations.All.Count, version);
    }

    [Fact]
    public async Task InitializeAsync_NewFile_CreatesAllTables()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var testDatabase = await TestDatabase.CreateAsync(cancellationToken);

        await using var connection = await testDatabase.Database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;";

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            names.Add(reader.GetString(0));
        }

        Assert.Equal(["chat_attachments", "chat_messages", "generations", "image_models", "runs", "sessions"], names);
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_DoesNotFail()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var testDatabase = await TestDatabase.CreateAsync(cancellationToken);

        await testDatabase.Database.InitializeAsync(cancellationToken);
        var secondDatabase = new SqliteDatabase(testDatabase.Database.DatabasePath);
        await secondDatabase.InitializeAsync(cancellationToken);

        await using var connection = await secondDatabase.OpenAsync(cancellationToken);
        var version = await SqliteDatabase.GetSchemaVersionAsync(connection, cancellationToken);

        Assert.Equal(Migrations.All.Count, version);
    }

    [Fact]
    public async Task InitializeAsync_SchemaNewerThanApp_ThrowsAndKeepsVersion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var testDatabase = await TestDatabase.CreateAsync(cancellationToken);
        var newerVersion = Migrations.All.Count + 2;
        await using (var connection = await testDatabase.Database.OpenAsync(cancellationToken))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA user_version = {newerVersion};";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var olderApp = new SqliteDatabase(testDatabase.Database.DatabasePath);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => olderApp.InitializeAsync(cancellationToken));

        Assert.Contains(newerVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), exception.Message);
        await using var check = await olderApp.OpenAsync(cancellationToken);
        Assert.Equal(newerVersion, await SqliteDatabase.GetSchemaVersionAsync(check, cancellationToken));
    }

    [Fact]
    public async Task OpenAsync_ForeignKeys_AreEnabled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var testDatabase = await TestDatabase.CreateAsync(cancellationToken);

        await using var connection = await testDatabase.Database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys;";
        var result = await command.ExecuteScalarAsync(cancellationToken);

        Assert.Equal(1L, result);
    }
}

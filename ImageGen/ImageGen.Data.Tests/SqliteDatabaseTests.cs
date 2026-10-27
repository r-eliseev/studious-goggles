using Microsoft.Data.Sqlite;

namespace ImageGen.Data.Tests;

public class SqliteDatabaseTests
{
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

        Assert.Equal(["chat_messages", "generations", "image_models", "runs", "sessions"], names);
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

using Microsoft.Data.Sqlite;

namespace ImageGen.Data.Tests;

/// <summary>Temporary database file that is deleted on dispose.</summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private TestDatabase(string directory)
    {
        Directory = directory;
        Database = new SqliteDatabase(Path.Combine(directory, SqliteDatabase.FileName));
    }

    public string Directory { get; }

    public SqliteDatabase Database { get; }

    public static async Task<TestDatabase> CreateAsync(CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), "imagegen-tests", Guid.NewGuid().ToString("N"));
        var testDatabase = new TestDatabase(directory);
        await testDatabase.Database.InitializeAsync(cancellationToken);
        return testDatabase;
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (System.IO.Directory.Exists(Directory))
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }

        return ValueTask.CompletedTask;
    }
}

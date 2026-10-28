using ImageGen.Core.Platform;
using Microsoft.Data.Sqlite;

namespace ImageGen.Data;

public sealed class SqliteDatabase
{
    public const string FileName = "imagegen.db";

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public SqliteDatabase(IAppPaths appPaths)
        : this(Path.Combine(appPaths.DataDirectory, FileName))
    {
    }

    public SqliteDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        DatabasePath = databasePath;
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
        }.ToString();
    }

    public string DatabasePath { get; }

    public string ConnectionString { get; }

    /// <summary>Opens a new connection. Call <see cref="InitializeAsync"/> once before the first use.</summary>
    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    /// <summary>Creates the folder and the file if needed and applies pending migrations. Safe to call many times.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            var directory = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var connection = await OpenAsync(cancellationToken);

            var version = await GetSchemaVersionAsync(connection, cancellationToken);
            if (version > Migrations.All.Count)
            {
                // Downgrade is not supported (ADR 0002): refuse to touch a schema this build does not know.
                throw new InvalidOperationException(
                    $"База создана более новой версией приложения (версия схемы {version}, известно {Migrations.All.Count}). Обновите приложение.");
            }

            await using (var wal = connection.CreateCommand())
            {
                wal.CommandText = "PRAGMA journal_mode = WAL;";
                await wal.ExecuteNonQueryAsync(cancellationToken);
            }

            for (var index = version; index < Migrations.All.Count; index++)
            {
                var migration = Migrations.All[index];
                await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
                await using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = migration.Script;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }

                await using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = $"PRAGMA user_version = {migration.Number};";
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    internal static async Task<int> GetSchemaVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}

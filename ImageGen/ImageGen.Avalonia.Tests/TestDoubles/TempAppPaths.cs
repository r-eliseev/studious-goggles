using ImageGen.Core.Platform;

namespace ImageGen.Avalonia.Tests.TestDoubles;

/// <summary>A data folder unique to one test, deleted on dispose (the database and images live there).</summary>
public sealed class TempAppPaths : IAppPaths, IDisposable
{
    public TempAppPaths()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), "imagegen-avalonia-" + Guid.NewGuid().ToString("N"));
    }

    public string DataDirectory { get; }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(DataDirectory))
        {
            Directory.Delete(DataDirectory, recursive: true);
        }
    }
}

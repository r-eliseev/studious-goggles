using ImageGen.Core.Services;
using ImageGen.Data;

namespace ImageGen.Presentation.Services;

/// <summary>Runs one-time startup work: creates or migrates the database and marks interrupted generations.</summary>
public sealed class AppInitializer(SqliteDatabase database, GenerationService generationService)
{
    private readonly Lock _lock = new();
    private Task? _initialization;

    /// <summary>The first call starts the work; later and concurrent calls await the same task.</summary>
    public Task EnsureInitializedAsync()
    {
        lock (_lock)
        {
            return _initialization ??= InitializeAsync();
        }
    }

    private async Task InitializeAsync()
    {
        await database.InitializeAsync(CancellationToken.None);
        await generationService.RecoverInterruptedAsync(CancellationToken.None);
    }
}

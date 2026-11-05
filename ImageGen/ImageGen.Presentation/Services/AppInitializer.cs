using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Data;

namespace ImageGen.Presentation.Services;

/// <summary>
/// Runs one-time startup work: creates or migrates the database and marks interrupted generations.
/// Taking the provider registry makes an invalid provider config stop the start (see <see cref="ProviderRegistry"/>).
/// </summary>
public sealed class AppInitializer(
    SqliteDatabase database,
    GenerationService generationService,
    ProviderRegistry providers,
    IImageFileStore fileStore,
    TimeProvider timeProvider)
{
    private readonly Lock _lock = new();
    private readonly ProviderRegistry _providers = providers;
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
        // Reading the registry validates the provider config; an invalid file stops the start here.
        _ = _providers.All;
        await database.InitializeAsync(CancellationToken.None);
        await Task.Run(() => fileStore.DeleteOrphanedTemporaryFiles(TimeSpan.FromHours(1), timeProvider.GetUtcNow()));
        await generationService.RecoverInterruptedAsync(CancellationToken.None);
    }
}

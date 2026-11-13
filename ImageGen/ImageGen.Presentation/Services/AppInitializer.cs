using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Data;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Presentation.Services;

/// <summary>
/// Runs one-time startup work: creates or migrates the database and marks interrupted generations.
/// Taking the provider registry makes an invalid provider config stop the start (see <see cref="ProviderRegistry"/>).
/// </summary>
public sealed class AppInitializer
{
    private readonly SqliteDatabase _database;
    private readonly GenerationService _generationService;
    private readonly ProviderRegistry _providers;
    private readonly IImageFileStore _fileStore;
    private readonly TimeProvider _timeProvider;
    private readonly Func<CancellationToken, Task> _refreshModelCatalogs;

    public AppInitializer(SqliteDatabase database, GenerationService generationService, ProviderRegistry providers,
        IImageFileStore fileStore, TimeProvider timeProvider, ModelCatalogRefreshService modelCatalogRefresh)
        : this(database, generationService, providers, fileStore, timeProvider, modelCatalogRefresh.RefreshStaleAsync)
    {
    }

    public AppInitializer(SqliteDatabase database, GenerationService generationService, ProviderRegistry providers,
        IImageFileStore fileStore, TimeProvider timeProvider)
        : this(database, generationService, providers, fileStore, timeProvider, _ => Task.CompletedTask)
    {
    }

    internal AppInitializer(SqliteDatabase database, GenerationService generationService, ProviderRegistry providers,
        IImageFileStore fileStore, TimeProvider timeProvider, Func<CancellationToken, Task> refreshModelCatalogs)
    {
        _database = database;
        _generationService = generationService;
        _providers = providers;
        _fileStore = fileStore;
        _timeProvider = timeProvider;
        _refreshModelCatalogs = refreshModelCatalogs;
    }

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
        // Reading the registry validates the provider config; an invalid file stops the start here.
        _ = _providers.All;
        await _database.InitializeAsync(CancellationToken.None);
        await Task.Run(() => _fileStore.DeleteOrphanedTemporaryFiles(TimeSpan.FromHours(1), _timeProvider.GetUtcNow()));
        await _generationService.RecoverInterruptedAsync(CancellationToken.None);
        _refreshModelCatalogs(CancellationToken.None).FireAndForgetSafeAsync();
    }
}

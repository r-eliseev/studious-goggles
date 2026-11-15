using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Presentation.Services;

/// <summary>
/// Runs one-time startup work: creates or migrates the database and marks interrupted generations.
/// Taking the provider registry makes an invalid provider config stop the start (see <see cref="ProviderRegistry"/>).
/// </summary>
public sealed class AppInitializer : IDisposable, IAsyncDisposable
{
    private static readonly TimeSpan RefreshShutdownTimeout = TimeSpan.FromSeconds(10);

    private readonly SqliteDatabase _database;
    private readonly GenerationService _generationService;
    private readonly ProviderRegistry _providers;
    private readonly IImageFileStore _fileStore;
    private readonly TimeProvider _timeProvider;
    private readonly Func<CancellationToken, Task> _refreshModelCatalogs;
    private readonly ILogger<AppInitializer> _logger;
    private readonly CancellationTokenSource _refreshCancellation = new();
    private readonly Lock _lock = new();
    private Task? _initialization;
    private Task? _refreshTask;
    private bool _disposed;

    public AppInitializer(SqliteDatabase database, GenerationService generationService, ProviderRegistry providers,
        IImageFileStore fileStore, TimeProvider timeProvider, ModelCatalogRefreshService modelCatalogRefresh, ILogger<AppInitializer> logger)
        : this(database, generationService, providers, fileStore, timeProvider, modelCatalogRefresh.RefreshStaleAsync, logger)
    {
    }

    public AppInitializer(SqliteDatabase database, GenerationService generationService, ProviderRegistry providers,
        IImageFileStore fileStore, TimeProvider timeProvider)
        : this(database, generationService, providers, fileStore, timeProvider, _ => Task.CompletedTask, NullLogger<AppInitializer>.Instance)
    {
    }

    internal AppInitializer(SqliteDatabase database, GenerationService generationService, ProviderRegistry providers,
        IImageFileStore fileStore, TimeProvider timeProvider, Func<CancellationToken, Task> refreshModelCatalogs, ILogger<AppInitializer>? logger = null)
    {
        _database = database;
        _generationService = generationService;
        _providers = providers;
        _fileStore = fileStore;
        _timeProvider = timeProvider;
        _refreshModelCatalogs = refreshModelCatalogs;
        _logger = logger ?? NullLogger<AppInitializer>.Instance;
    }

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
        lock (_lock)
        {
            if (!_disposed)
            {
                _refreshTask = RefreshModelCatalogsAsync(_refreshCancellation.Token);
            }
        }
    }

    public void Dispose()
    {
        if (!TryBeginDisposal(out var refreshTask))
        {
            return;
        }

        try
        {
            if (!refreshTask.Wait(RefreshShutdownTimeout))
            {
                _logger.LogDebug("Model catalog refresh did not stop within {TimeoutSeconds} seconds during disposal.", RefreshShutdownTimeout.TotalSeconds);
            }
        }
        catch (AggregateException exception)
        {
            _logger.LogDebug(exception, "Model catalog refresh failed during disposal.");
        }
        finally
        {
            _refreshCancellation.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!TryBeginDisposal(out var refreshTask))
        {
            return;
        }

        try
        {
            await refreshTask.WaitAsync(RefreshShutdownTimeout).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Model catalog refresh did not finish cleanly during disposal.");
        }
        finally
        {
            _refreshCancellation.Dispose();
        }
    }

    private bool TryBeginDisposal(out Task refreshTask)
    {
        lock (_lock)
        {
            if (_initialization is null || _disposed)
            {
                refreshTask = Task.CompletedTask;
                return false;
            }

            _disposed = true;
            _refreshCancellation.Cancel();
            refreshTask = _refreshTask ?? Task.CompletedTask;
            return true;
        }
    }

    private async Task RefreshModelCatalogsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _refreshModelCatalogs(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Model catalog refresh did not finish cleanly.");
        }
    }
}

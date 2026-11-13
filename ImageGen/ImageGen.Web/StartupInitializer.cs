using ImageGen.Core.Services;
using ImageGen.Data;
using Microsoft.Extensions.Hosting;

namespace ImageGen.Web;

public sealed class StartupInitializer(SqliteDatabase database, GenerationService generations, ModelCatalogRefreshService modelCatalogRefresh) : IHostedService
{
    private readonly CancellationTokenSource _refreshCancellation = new();
    private Task? _refreshTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await database.InitializeAsync(cancellationToken);
        await generations.RecoverInterruptedAsync(cancellationToken);
        _refreshTask = modelCatalogRefresh.RefreshStaleAsync(_refreshCancellation.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _refreshCancellation.CancelAsync();
        if (_refreshTask is not null)
        {
            try
            {
                await _refreshTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (_refreshCancellation.IsCancellationRequested)
            {
            }
        }
    }
}

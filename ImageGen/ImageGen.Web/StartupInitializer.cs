using ImageGen.Core.Services;
using ImageGen.Data;
using Microsoft.Extensions.Hosting;

namespace ImageGen.Web;

public sealed class StartupInitializer(SqliteDatabase database, GenerationService generations) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await database.InitializeAsync(cancellationToken);
        await generations.RecoverInterruptedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

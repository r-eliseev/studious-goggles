using ImageGen.Avalonia.Views;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Acceptance.Tests.Support;

public sealed class AppWorldTests
{
    [Fact]
    public async Task RestartAsync_ReusesDataFolderAndSecretStore()
    {
        await HeadlessApp.RunAsync(async cancellationToken =>
        {
            var world = new AppWorld();
            try
            {
                var initialDataDirectory = world.DataDirectory;
                await world.Secrets.SetAsync(SecretNames.OpenRouterApiKey, "e2e-mock-key", cancellationToken);
                await world.StartAsync(cancellationToken);
                var session = await world.Provider.GetRequiredService<SessionService>().CreateAsync(cancellationToken);

                await world.RestartAsync(cancellationToken);

                var sessions = await world.Provider.GetRequiredService<SessionService>().ListAsync(cancellationToken);
                var savedKey = await world.Secrets.GetAsync(SecretNames.OpenRouterApiKey, cancellationToken);
                Assert.Equal(initialDataDirectory, world.DataDirectory);
                Assert.Contains(sessions, candidate => candidate.Id == session.Id);
                Assert.Equal("e2e-mock-key", savedKey);
                Assert.IsNotType<ApiKeyView>(world.Window.PageArea.Content);
                return true;
            }
            finally
            {
                await world.DisposeAsync();
            }
        }, TestContext.Current.CancellationToken);
    }
}

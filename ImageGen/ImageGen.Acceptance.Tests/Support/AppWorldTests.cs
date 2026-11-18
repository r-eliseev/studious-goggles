using Avalonia.Threading;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Acceptance.Tests.Support;

public sealed class AppWorldTests
{
    [Fact]
    public async Task WaitUntilAsync_EvaluatesConditionOnAvaloniaThread()
    {
        var world = new AppWorld();
        try
        {
            await Task.Run(
                () => world.WaitUntilAsync(
                    () => Dispatcher.UIThread.CheckAccess(),
                    "condition to run on the Avalonia thread",
                    TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken);
        }
        finally
        {
            await HeadlessApp.RunAsync(async cancellationToken =>
            {
                await world.DisposeAsync();
                return true;
            }, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task ReadLogFile_WhileRunningAndAfterRestart_AllowsSharedReadAndCleansUp()
    {
        var dataDirectory = string.Empty;

        await HeadlessApp.RunAsync(async cancellationToken =>
        {
            var world = new AppWorld();
            dataDirectory = world.DataDirectory;
            try
            {
                await world.StartAsync(cancellationToken);
                var logFile = Assert.Single(Directory.GetFiles(Path.Combine(dataDirectory, "logs"), "imagegen-*.log"));
                Assert.Contains("Navigation to sessions", world.ReadLogFile(logFile), StringComparison.Ordinal);

                await world.RestartAsync(cancellationToken);

                Assert.Contains("Navigation to sessions", world.ReadLogFile(logFile), StringComparison.Ordinal);
                return true;
            }
            finally
            {
                await world.DisposeAsync();
            }
        }, TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(dataDirectory));
    }

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

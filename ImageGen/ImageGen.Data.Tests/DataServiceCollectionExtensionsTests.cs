using ImageGen.Core.Platform;
using ImageGen.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Data.Tests;

public class DataServiceCollectionExtensionsTests
{
    [Fact]
    public void AddImageGenData_WithAppPaths_ResolvesAllStorageServices()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.DataDirectory.Returns(Path.GetTempPath());
        var services = new ServiceCollection();
        services.AddSingleton(paths);
        services.AddLogging();

        services.AddImageGenData();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<SqliteDatabase>());
        Assert.NotNull(provider.GetRequiredService<ISessionRepository>());
        Assert.NotNull(provider.GetRequiredService<IChatMessageRepository>());
        Assert.NotNull(provider.GetRequiredService<IRunRepository>());
        Assert.NotNull(provider.GetRequiredService<IImageModelRepository>());
        Assert.NotNull(provider.GetRequiredService<IImageFileStore>());
    }
}

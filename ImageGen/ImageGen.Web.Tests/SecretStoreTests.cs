using ImageGen.Core.Platform;
using ImageGen.Web.Secrets;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Web.Tests;

public sealed class SecretStoreTests
{
    [Fact]
    public async Task DataProtectionSecretStore_PersistsEncryptedValueAcrossRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "imagegen-web-tests", Guid.NewGuid().ToString("N"));
        var dataDirectory = Path.Combine(directory, "data");
        var keysDirectory = Path.Combine(dataDirectory, "keys");
        Directory.CreateDirectory(keysDirectory);
        try
        {
            using (var firstProvider = CreateProvider(keysDirectory))
            {
                var store = new DataProtectionSecretStore(new TestPaths(dataDirectory), firstProvider.GetRequiredService<IDataProtectionProvider>());
                await store.SetAsync("openrouter", "test-secret-value", TestContext.Current.CancellationToken);
            }

            var persisted = await File.ReadAllTextAsync(Path.Combine(dataDirectory, "secrets.json"), TestContext.Current.CancellationToken);
            Assert.DoesNotContain("test-secret-value", persisted, StringComparison.Ordinal);

            using var restartedProvider = CreateProvider(keysDirectory);
            var restartedStore = new DataProtectionSecretStore(new TestPaths(dataDirectory), restartedProvider.GetRequiredService<IDataProtectionProvider>());
            Assert.Equal("test-secret-value", await restartedStore.GetAsync("openrouter", TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ServiceProvider CreateProvider(string keysDirectory) => new ServiceCollection()
        .AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory))
        .SetApplicationName("ImageGen")
        .Services
        .BuildServiceProvider();

    private sealed class TestPaths(string dataDirectory) : IAppPaths
    {
        public string DataDirectory { get; } = dataDirectory;
    }
}

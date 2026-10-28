using System.Runtime.Versioning;
using System.Text;
using ImageGen.Avalonia.Services.Secrets;

namespace ImageGen.Avalonia.Tests.Services.Secrets;

public class DpapiSecretBackendTests
{
    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task SetGetRemove_OnWindows_RoundTripsWithoutPlaintextOnDisk()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("DPAPI работает только на Windows.");
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "imagegen-dpapi-" + Guid.NewGuid().ToString("N"));
        try
        {
            var backend = new DpapiSecretBackend(directory);
            const string secret = "sk-or-dpapi-probe";

            await backend.SetAsync("openrouter_api_key", secret, cancellationToken);

            var onDisk = await File.ReadAllBytesAsync(SecretFileNames.PathFor(directory, "openrouter_api_key"), cancellationToken);
            Assert.DoesNotContain(secret, Encoding.UTF8.GetString(onDisk), StringComparison.Ordinal);
            Assert.Equal(secret, await backend.GetAsync("openrouter_api_key", cancellationToken));

            await backend.RemoveAsync("openrouter_api_key", cancellationToken);
            Assert.Null(await backend.GetAsync("openrouter_api_key", cancellationToken));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}

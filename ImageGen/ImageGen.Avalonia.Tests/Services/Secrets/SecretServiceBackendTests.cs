using ImageGen.Avalonia.Services.Secrets;

namespace ImageGen.Avalonia.Tests.Services.Secrets;

/// <summary>
/// Runs against the Secret Service of the current D-Bus session (gnome-keyring, KWallet). The "session" collection is used
/// so that no prompt is needed. Skipped when there is no such service (for example in CI).
/// </summary>
public class SecretServiceBackendTests
{
    [Fact]
    public async Task SetGetRemove_SecondInstanceReadsTheSameValue()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var backend = new SecretServiceBackend("session");
        if (!await backend.IsAvailableAsync(cancellationToken))
        {
            Assert.Skip("Secret Service с коллекцией 'session' недоступен: нет сессии D-Bus или службы.");
        }

        var name = "test_" + Guid.NewGuid().ToString("N");
        try
        {
            await backend.SetAsync(name, "sk-or-secret-service-probe", cancellationToken);

            // A new instance has no state of its own: the value must come from the service.
            Assert.Equal("sk-or-secret-service-probe", await new SecretServiceBackend("session").GetAsync(name, cancellationToken));
        }
        finally
        {
            await backend.RemoveAsync(name, cancellationToken);
        }

        Assert.Null(await backend.GetAsync(name, cancellationToken));
    }
}

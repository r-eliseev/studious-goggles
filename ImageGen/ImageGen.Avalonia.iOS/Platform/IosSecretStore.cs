using System.Text;
using ImageGen.Avalonia.Services.Secrets;
using ImageGen.Core.Platform;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.Logging;
using Security;

namespace ImageGen.Avalonia.Platform;

/// <summary>Stores provider secrets in the iOS Keychain.</summary>
public sealed class IosSecretStore : ISecretStore, ISecretPersistence
{
    private const string ServiceName = "com.eliseev.imagegen.avalonia";
    private ILogger<IosSecretStore>? _logger;

    internal void AttachLogger(ILogger<IosSecretStore> logger) =>
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Task<bool> IsPersistentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }

    public Task<string?> GetAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var record = SecKeyChain.QueryAsRecord(Query(name), out var status);
            if (status == SecStatusCode.ItemNotFound)
            {
                return Task.FromResult<string?>(null);
            }

            if (status != SecStatusCode.Success || record?.ValueData is not { } data)
            {
                WarnKeychainFailure(status);
                return Task.FromResult<string?>(null);
            }

            return Task.FromResult<string?>(Encoding.UTF8.GetString(data.ToArray()));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            WarnKeychainFailure(null);
            return Task.FromResult<string?>(null);
        }
    }

    public Task SetAsync(string name, string value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        var query = Query(name);
        var record = new SecRecord(SecKind.GenericPassword)
        {
            Service = ServiceName,
            Account = name,
            ValueData = Foundation.NSData.FromString(value, Foundation.NSStringEncoding.UTF8),
            Accessible = SecAccessible.AfterFirstUnlockThisDeviceOnly,
        };
        var status = SecKeyChain.Update(query, record);
        if (status == SecStatusCode.ItemNotFound)
        {
            status = SecKeyChain.Add(record);
        }

        if (status != SecStatusCode.Success)
        {
            WarnKeychainFailure(status);
            throw new InvalidOperationException("Could not save the secret to the iOS Keychain.");
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var status = SecKeyChain.Remove(Query(name));
        if (status is not (SecStatusCode.Success or SecStatusCode.ItemNotFound))
        {
            WarnKeychainFailure(status);
            throw new InvalidOperationException("Could not remove the secret from the iOS Keychain.");
        }

        return Task.CompletedTask;
    }

    private static SecRecord Query(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new SecRecord(SecKind.GenericPassword) { Service = ServiceName, Account = name };
    }

    private void WarnKeychainFailure(SecStatusCode? status)
    {
        if (_logger is { } logger)
        {
            logger.LogWarning("iOS Keychain operation failed with status {Status}.", status?.ToString() ?? "unknown");
        }
        else
        {
            System.Diagnostics.Trace.TraceWarning("iOS Keychain operation failed with status {0}.", status?.ToString() ?? "unknown");
        }
    }
}

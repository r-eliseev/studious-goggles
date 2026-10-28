using System.Text;
using Tmds.DBus;

namespace ImageGen.Avalonia.Services.Secrets;

/// <summary>
/// Linux: the freedesktop Secret Service over the session D-Bus (gnome-keyring, KWallet), ADR 0008.
/// Items are found by the attributes <c>service=imagegen</c> and <c>account=&lt;name&gt;</c>. A request that would need a
/// prompt (locked collection, new collection) fails: there is no window to answer it.
/// </summary>
public sealed class SecretServiceBackend(string collectionAlias = "default") : ISecretBackend
{
    private const string BusName = "org.freedesktop.secrets";
    private const string Application = "imagegen";
    private const string ContentType = "text/plain; charset=utf8";
    private static readonly ObjectPath ServicePath = new("/org/freedesktop/secrets");

    private readonly Lock _lock = new();
    private Task<Connection>? _connection;

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var (service, _, _) = await OpenAsync();
            return await service.ReadAliasAsync(collectionAlias) != ObjectPath.Root;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<string?> GetAsync(string name, CancellationToken cancellationToken)
    {
        var (service, _, session) = await OpenAsync();
        var (unlocked, _) = await service.SearchItemsAsync(Attributes(name));
        if (unlocked.Length == 0)
        {
            return null;
        }

        var secrets = await service.GetSecretsAsync(unlocked, session);
        return Encoding.UTF8.GetString(secrets[unlocked[0]].Value);
    }

    public async Task SetAsync(string name, string value, CancellationToken cancellationToken)
    {
        var (service, connection, session) = await OpenAsync();
        var collectionPath = await service.ReadAliasAsync(collectionAlias);
        if (collectionPath == ObjectPath.Root)
        {
            throw new InvalidOperationException($"В Secret Service нет коллекции «{collectionAlias}».");
        }

        var collection = connection.CreateProxy<ISecretCollection>(BusName, collectionPath);
        var properties = new Dictionary<string, object>
        {
            ["org.freedesktop.Secret.Item.Label"] = $"ImageGen {name}",
            ["org.freedesktop.Secret.Item.Attributes"] = Attributes(name),
        };
        var (_, prompt) = await collection.CreateItemAsync(
            properties,
            (session, Array.Empty<byte>(), Encoding.UTF8.GetBytes(value), ContentType),
            replace: true);
        if (prompt != ObjectPath.Root)
        {
            throw new InvalidOperationException("Secret Service запросил подтверждение: ключ не сохранён.");
        }
    }

    public async Task RemoveAsync(string name, CancellationToken cancellationToken)
    {
        var (service, connection, _) = await OpenAsync();
        var (unlocked, _) = await service.SearchItemsAsync(Attributes(name));
        foreach (var item in unlocked)
        {
            await connection.CreateProxy<ISecretItem>(BusName, item).DeleteAsync();
        }
    }

    private static IDictionary<string, string> Attributes(string name) =>
        new Dictionary<string, string> { ["service"] = Application, ["account"] = name };

    private async Task<(ISecretService Service, Connection Connection, ObjectPath Session)> OpenAsync()
    {
        var connection = await ConnectAsync();
        var service = connection.CreateProxy<ISecretService>(BusName, ServicePath);
        var (_, session) = await service.OpenSessionAsync("plain", string.Empty);
        return (service, connection, session);
    }

    private Task<Connection> ConnectAsync()
    {
        Task<Connection> connecting;
        lock (_lock)
        {
            _connection ??= ConnectCoreAsync();
            connecting = _connection;
        }

        // A failed connection is not kept: the next call tries again.
        connecting.ContinueWith(
            task =>
            {
                if (task.IsFaulted)
                {
                    lock (_lock)
                    {
                        if (ReferenceEquals(_connection, connecting))
                        {
                            _connection = null;
                        }
                    }
                }
            },
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        return connecting;
    }

    private static async Task<Connection> ConnectCoreAsync()
    {
        var connection = Connection.Session;
        await connection.ConnectAsync();
        return connection;
    }
}

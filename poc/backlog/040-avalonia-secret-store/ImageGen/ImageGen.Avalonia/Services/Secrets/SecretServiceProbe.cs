using System.Text;
using Tmds.DBus;

namespace ImageGen.Avalonia.Services.Secrets;

// Probe for task 040: the freedesktop Secret Service over D-Bus (gnome-keyring, KWallet).
// Not part of the solution. Result and conclusions: the comments of task 040 and docs/decisions/0008-desktop-secret-storage.md.

[DBusInterface("org.freedesktop.Secret.Service")]
public interface ISecretService : IDBusObject
{
    Task<(object Output, ObjectPath Session)> OpenSessionAsync(string algorithm, object input);

    Task<ObjectPath> ReadAliasAsync(string name);

    Task<(ObjectPath[] Unlocked, ObjectPath[] Locked)> SearchItemsAsync(IDictionary<string, string> attributes);

    Task<IDictionary<ObjectPath, (ObjectPath Session, byte[] Parameters, byte[] Value, string ContentType)>> GetSecretsAsync(
        ObjectPath[] items, ObjectPath session);
}

[DBusInterface("org.freedesktop.Secret.Collection")]
public interface ISecretCollection : IDBusObject
{
    Task<(ObjectPath Item, ObjectPath Prompt)> CreateItemAsync(
        IDictionary<string, object> properties,
        (ObjectPath Session, byte[] Parameters, byte[] Value, string ContentType) secret,
        bool replace);
}

[DBusInterface("org.freedesktop.Secret.Item")]
public interface ISecretItem : IDBusObject
{
    Task<ObjectPath> DeleteAsync();
}

public static class SecretServiceProbe
{
    private const string Bus = "org.freedesktop.secrets";
    private static readonly ObjectPath ServicePath = new("/org/freedesktop/secrets");

    public static async Task RunAsync(string alias, string account, string secret)
    {
        var connection = Connection.Session;
        await connection.ConnectAsync();
        var service = connection.CreateProxy<ISecretService>(Bus, ServicePath);
        var (_, session) = await service.OpenSessionAsync("plain", string.Empty);
        Console.WriteLine($"session={session}");

        var collectionPath = await service.ReadAliasAsync(alias);
        Console.WriteLine($"alias {alias} -> {collectionPath}");
        if (collectionPath == ObjectPath.Root)
        {
            Console.WriteLine("no collection for this alias: Secret Service is not usable for persistence");
            return;
        }

        var collection = connection.CreateProxy<ISecretCollection>(Bus, collectionPath);
        var attributes = new Dictionary<string, string> { ["service"] = "imagegen", ["account"] = account };
        var properties = new Dictionary<string, object>
        {
            ["org.freedesktop.Secret.Item.Label"] = $"ImageGen {account}",
            ["org.freedesktop.Secret.Item.Attributes"] = attributes,
        };
        var (item, prompt) = await collection.CreateItemAsync(
            properties,
            (session, Array.Empty<byte>(), Encoding.UTF8.GetBytes(secret), "text/plain; charset=utf8"),
            replace: true);
        Console.WriteLine($"created item={item} prompt={prompt}");
        if (prompt != ObjectPath.Root)
        {
            Console.WriteLine("store needs a prompt (locked collection): not usable without UI");
            return;
        }

        var (unlocked, locked) = await service.SearchItemsAsync(attributes);
        Console.WriteLine($"search unlocked={unlocked.Length} locked={locked.Length}");
        var secrets = await service.GetSecretsAsync(unlocked, session);
        var read = secrets.TryGetValue(item, out var value) ? Encoding.UTF8.GetString(value.Value) : null;
        Console.WriteLine($"read matches={read == secret}");

        var deletePrompt = await connection.CreateProxy<ISecretItem>(Bus, item).DeleteAsync();
        var (_, locked2) = await service.SearchItemsAsync(attributes);
        Console.WriteLine($"deleted prompt={deletePrompt} remaining={locked2.Length + (await service.SearchItemsAsync(attributes)).Unlocked.Length}");
    }
}

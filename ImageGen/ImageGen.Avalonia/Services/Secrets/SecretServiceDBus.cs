using Tmds.DBus;

namespace ImageGen.Avalonia.Services.Secrets;

// D-Bus interfaces of the freedesktop Secret Service (org.freedesktop.secrets). Verified on Linux with gnome-keyring
// (docs/decisions/0008-desktop-secret-storage.md). Only the members that SecretServiceBackend uses are declared.

[DBusInterface("org.freedesktop.Secret.Service")]
public interface ISecretService : IDBusObject
{
    Task<(object Output, ObjectPath Session)> OpenSessionAsync(string algorithm, object input);

    Task<ObjectPath> ReadAliasAsync(string name);

    Task<(ObjectPath[] Unlocked, ObjectPath[] Locked)> SearchItemsAsync(IDictionary<string, string> attributes);

    Task<IDictionary<ObjectPath, (ObjectPath Session, byte[] Parameters, byte[] Value, string ContentType)>> GetSecretsAsync(
        ObjectPath[] items,
        ObjectPath session);
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

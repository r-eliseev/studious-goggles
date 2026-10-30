using System.Text;
using Android.Security;
using Android.Security.Keystore;
using ImageGen.Avalonia.Services.Secrets;
using ImageGen.Core.Platform;
using ImageGen.Presentation.Services;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;

namespace ImageGen.Avalonia.Platform;

public sealed class AndroidSecretStore(string dataDirectory) : ISecretStore, ISecretPersistence
{
    private const string Alias = "ImageGenSecrets";
    public bool HasStoredSecret(string name) => File.Exists(SecretFileNames.PathFor(dataDirectory, name));

    private KeyStore Store()
    {
        var store = KeyStore.GetInstance("AndroidKeyStore")!;
        store.Load(null);
        if (!store.ContainsAlias(Alias))
        {
            var generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, "AndroidKeyStore")!;
            var spec = new KeyGenParameterSpec.Builder(Alias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
                .SetBlockModes(KeyProperties.BlockModeGcm).SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone).SetKeySize(256).Build();
            generator.Init(spec);
            generator.GenerateKey();
        }
        return store;
    }
    public Task<bool> IsPersistentAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    public Task<string?> GetAsync(string name, CancellationToken cancellationToken)
    {
        var path = SecretFileNames.PathFor(dataDirectory, name);
        if (!File.Exists(path)) return Task.FromResult<string?>(null);
        try
        {
            var bytes = File.ReadAllBytes(path);
            var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
            var iv = bytes[..12];
            cipher.Init(Javax.Crypto.CipherMode.DecryptMode, Store().GetKey(Alias, null), new GCMParameterSpec(128, iv));
            var plain = cipher.DoFinal(bytes, 12, bytes.Length - 12)!;
            return Task.FromResult<string?>(Encoding.UTF8.GetString(plain!));
        }
        catch { return Task.FromResult<string?>(null); }
    }
    public Task SetAsync(string name, string value, CancellationToken cancellationToken)
    {
        var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
        cipher.Init(Javax.Crypto.CipherMode.EncryptMode, Store().GetKey(Alias, null));
        var encrypted = cipher.DoFinal(Encoding.UTF8.GetBytes(value))!;
        var iv = cipher.GetIV()!;
        var path = SecretFileNames.PathFor(dataDirectory, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [.. iv, .. encrypted]);
        return Task.CompletedTask;
    }
    public Task RemoveAsync(string name, CancellationToken cancellationToken)
    {
        var path = SecretFileNames.PathFor(dataDirectory, name);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}

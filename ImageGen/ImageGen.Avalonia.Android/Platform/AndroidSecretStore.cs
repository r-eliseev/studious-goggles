using System.Security.Cryptography;
using System.Text;
using Android.Security;
using Android.Security.Keystore;
using ImageGen.Avalonia.Services.Secrets;
using ImageGen.Core.Platform;
using ImageGen.Presentation.Services;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using Microsoft.Extensions.Logging;

namespace ImageGen.Avalonia.Platform;

/// <summary>Stores AES-GCM encrypted secret files with a non-exportable Android Keystore key.</summary>
public sealed class AndroidSecretStore(string dataDirectory) : ISecretStore, ISecretPersistence
{
    private const string Alias = "OmniliteAvaloniaSecrets";
    private const int InitializationVectorLength = 12;
    private const int AuthenticationTagLength = 16;
    private ILogger<AndroidSecretStore>? _logger;

    internal void AttachLogger(ILogger<AndroidSecretStore> logger) =>
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Task<bool> IsPersistentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }

    public async Task<string?> GetAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = SecretFileNames.PathFor(dataDirectory, name);
        if (!File.Exists(path))
        {
            return null;
        }

        byte[] encrypted;
        try
        {
            encrypted = await File.ReadAllBytesAsync(path, cancellationToken);
        }
        catch (IOException)
        {
            WarnUnreadableSecret();
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            WarnUnreadableSecret();
            return null;
        }

        if (encrypted.Length < InitializationVectorLength + AuthenticationTagLength)
        {
            WarnUnreadableSecret();
            return null;
        }

        var plaintext = Array.Empty<byte>();
        try
        {
            var iv = encrypted.AsSpan(0, InitializationVectorLength).ToArray();
            var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
            cipher.Init(Javax.Crypto.CipherMode.DecryptMode, StoreKey(), new GCMParameterSpec(AuthenticationTagLength * 8, iv));
            plaintext = cipher.DoFinal(encrypted, InitializationVectorLength, encrypted.Length - InitializationVectorLength)!;
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (Exception exception) when (exception is Java.Lang.Exception or CryptographicException or ArgumentException)
        {
            WarnUnreadableSecret();
            return null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(encrypted);
        }
    }

    public async Task SetAsync(string name, string value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        var path = SecretFileNames.PathFor(dataDirectory, name);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        var plaintext = Encoding.UTF8.GetBytes(value);
        byte[]? encrypted = null;
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
            cipher.Init(Javax.Crypto.CipherMode.EncryptMode, StoreKey());
            var ciphertext = cipher.DoFinal(plaintext)!;
            var iv = cipher.GetIV()!;
            encrypted = new byte[iv.Length + ciphertext.Length];
            Buffer.BlockCopy(iv, 0, encrypted, 0, iv.Length);
            Buffer.BlockCopy(ciphertext, 0, encrypted, iv.Length, ciphertext.Length);
            await File.WriteAllBytesAsync(temporaryPath, encrypted, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (encrypted is not null)
            {
                CryptographicOperations.ZeroMemory(encrypted);
            }

            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public Task RemoveAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = SecretFileNames.PathFor(dataDirectory, name);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private static IKey StoreKey()
    {
        var store = KeyStore.GetInstance("AndroidKeyStore")!;
        store.Load(null);
        if (store.ContainsAlias(Alias))
        {
            return store.GetKey(Alias, null)!;
        }

        var generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, "AndroidKeyStore")!;
        var parameters = new KeyGenParameterSpec.Builder(Alias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
            .SetBlockModes(KeyProperties.BlockModeGcm)
            .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
            .SetKeySize(256)
            .Build();
        generator.Init(parameters);
        return generator.GenerateKey()!;
    }

    private void WarnUnreadableSecret()
    {
        const string message = "Could not decrypt a saved secret; ignoring the stored value.";
        if (_logger is { } logger)
        {
            logger.LogWarning("{Message}", message);
        }
        else
        {
            global::Android.Util.Log.Warn("OmniliteSecrets", message);
        }
    }
}

using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using Lucia.Companion.Connection;
using System.Security.Cryptography;
using CipherMode = Javax.Crypto.CipherMode;

namespace Lucia.Companion.Android.Security;

/// <summary>App-scoped Android Keystore AES-GCM. Key material is never exported or written to the vault.</summary>
public sealed class AndroidDeviceSecretProtector : IDeviceSecretProtector
{
    private const string KeyStoreName = "AndroidKeyStore";
    private const int NonceBytes = 12;
    private const int TagBytes = 16;
    private static readonly object KeyGate = new();
    private static readonly byte[] Purpose = "Lucia.Companion.DeviceSession.v1"u8.ToArray();
    private readonly string alias;

    public AndroidDeviceSecretProtector() : this("Lucia.Companion.DeviceSession.v1") { }
    internal AndroidDeviceSecretProtector(string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        this.alias = alias;
    }

    public byte[] Protect(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        try
        {
            using var key = GetKey(create: true);
            using var cipher = Cipher.GetInstance("AES/GCM/NoPadding") ?? throw new CryptographicException();
            // Android generates a random IV. A caller-selected IV is deliberately never used for encryption.
            cipher.Init(CipherMode.EncryptMode, key);
            cipher.UpdateAAD(Purpose);
            var nonce = cipher.GetIV() ?? throw new CryptographicException();
            var sealedBytes = cipher.DoFinal(plaintext) ?? throw new CryptographicException();
            if (nonce.Length != NonceBytes || sealedBytes.Length != plaintext.Length + TagBytes) { throw new CryptographicException(); }
            var envelope = new byte[1 + NonceBytes + sealedBytes.Length];
            envelope[0] = 1;
            nonce.CopyTo(envelope, 1);
            sealedBytes.CopyTo(envelope, 1 + NonceBytes);
            return envelope;
        }
        catch (Java.Lang.Exception) { throw new CryptographicException("Android device-key protection failed."); }
    }

    public byte[] Unprotect(byte[] ciphertext)
    {
        if (ciphertext is null || ciphertext.Length < 1 + NonceBytes + TagBytes || ciphertext[0] != 1) { throw new CryptographicException(); }
        try
        {
            using var key = GetKey(create: false);
            using var cipher = Cipher.GetInstance("AES/GCM/NoPadding") ?? throw new CryptographicException();
            using var parameters = new GCMParameterSpec(TagBytes * 8, ciphertext[1..(1 + NonceBytes)]);
            cipher.Init(CipherMode.DecryptMode, key, parameters);
            cipher.UpdateAAD(Purpose);
            return cipher.DoFinal(ciphertext[(1 + NonceBytes)..]) ?? throw new CryptographicException();
        }
        catch (Java.Lang.Exception) { throw new CryptographicException("The Android device key is unavailable or the protected connection is invalid."); }
    }

    private IKey GetKey(bool create)
    {
        lock (KeyGate)
        {
            using var store = KeyStore.GetInstance(KeyStoreName) ?? throw new CryptographicException();
            store.Load(null);
            if (store.GetKey(alias, null) is { } existing)
            {
                if (existing.Algorithm == "AES") { return existing; }
                existing.Dispose();
                throw new CryptographicException();
            }
            // Reading missing/unusable storage must never silently replace its key.
            if (!create) { throw new CryptographicException(); }
            using var generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes!, KeyStoreName) ?? throw new CryptographicException();
            using var builder = new KeyGenParameterSpec.Builder(alias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt);
            builder.SetKeySize(256);
            builder.SetBlockModes(KeyProperties.BlockModeGcm!);
            builder.SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone!);
            builder.SetRandomizedEncryptionRequired(true);
            // App sandbox and device security protect at-rest data. Background refresh does not prompt for biometrics.
            builder.SetUserAuthenticationRequired(false);
            using var specification = builder.Build() ?? throw new CryptographicException();
            generator.Init(specification);
            return generator.GenerateKey() ?? throw new CryptographicException();
        }
    }
}

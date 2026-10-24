using System.Security.Cryptography;
using Lucia.Companion.Connection;

namespace Lucia.Companion.Tests;

/// <summary>Ephemeral test key only, not a platform-storage qualification.</summary>
internal sealed class TestSecretProtector : IDeviceSecretProtector, IDisposable
{
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
    public int? FailOnProtect { get; set; }
    public int ProtectCalls { get; private set; }
    public byte[] Protect(byte[] plaintext)
    {
        if (++ProtectCalls == FailOnProtect) { throw new CryptographicException("Injected test fault."); }
        var output = new byte[12 + 16 + plaintext.Length];
        RandomNumberGenerator.Fill(output.AsSpan(0, 12));
        using var cipher = new AesGcm(key, 16);
        cipher.Encrypt(output.AsSpan(0, 12), plaintext, output.AsSpan(28), output.AsSpan(12, 16));
        return output;
    }
    public byte[] Unprotect(byte[] ciphertext)
    {
        if (ciphertext.Length < 28) { throw new CryptographicException(); }
        var output = new byte[ciphertext.Length - 28];
        using var cipher = new AesGcm(key, 16);
        cipher.Decrypt(ciphertext.AsSpan(0, 12), ciphertext.AsSpan(28), ciphertext.AsSpan(12, 16), output);
        return output;
    }
    public void Dispose() => CryptographicOperations.ZeroMemory(key);
}

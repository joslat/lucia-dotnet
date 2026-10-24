namespace Lucia.Companion.Connection;

/// <summary>Platform-owned key protection. Implementations must authenticate ciphertext, never fall back to plaintext.</summary>
public interface IDeviceSecretProtector
{
    byte[] Protect(byte[] plaintext);
    byte[] Unprotect(byte[] ciphertext);
}

using System.Runtime.Versioning;
using System.Security.Cryptography;
using Lucia.Companion.Connection;

namespace Lucia.Companion.Desktop.Security;

[SupportedOSPlatform("windows")]
public sealed class WindowsDeviceSecretProtector : IDeviceSecretProtector
{
    private static readonly byte[] Purpose = "Lucia.Companion.DeviceSession.v1"u8.ToArray();
    public byte[] Protect(byte[] plaintext) => ProtectedData.Protect(plaintext, Purpose, DataProtectionScope.CurrentUser);
    public byte[] Unprotect(byte[] ciphertext) => ProtectedData.Unprotect(ciphertext, Purpose, DataProtectionScope.CurrentUser);
}

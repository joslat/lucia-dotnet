using Lucia.Companion.Connection;
using Lucia.Companion.Desktop.Security;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class SessionVaultTests
{
    [Fact]
    public async Task VaultStoresOnlyCiphertextAndSerializesSeparateInstances()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-session-vault-");
        using var protector = new TestSecretProtector();
        var first = new ProtectedSessionVault(directory.FullName, protector);
        var second = new ProtectedSessionVault(directory.FullName, protector);
        try
        {
            await using (var lease = await first.OpenAsync(TestContext.Current.CancellationToken))
            {
                Assert.Null(await lease.ReadAsync(TestContext.Current.CancellationToken));
                await lease.SaveAsync(Record(SessionPhase.Renewing), TestContext.Current.CancellationToken);
                using var waiting = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second.OpenAsync(waiting.Token));
            }
            var raw = await File.ReadAllBytesAsync(Path.Combine(directory.FullName, "session.bin"), TestContext.Current.CancellationToken);
            Assert.DoesNotContain("lcr_", System.Text.Encoding.UTF8.GetString(raw));
            Assert.DoesNotContain("private-home", System.Text.Encoding.UTF8.GetString(raw));
            await using (var reopened = await second.OpenAsync(TestContext.Current.CancellationToken))
            {
                var record = Assert.IsType<StoredDeviceSession>(await reopened.ReadAsync(TestContext.Current.CancellationToken));
                Assert.Equal(SessionPhase.Renewing, record.Phase);
                Assert.Equal("private-home", record.HomeId);
                await reopened.ClearAsync(TestContext.Current.CancellationToken);
                Assert.Null(await reopened.ReadAsync(TestContext.Current.CancellationToken));
            }
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task CorruptCiphertextIsPreservedAndCannotBecomeAnEmptyConnection()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-session-corrupt-");
        using var protector = new TestSecretProtector();
        var vault = new ProtectedSessionVault(directory.FullName, protector);
        try
        {
            await using var lease = await vault.OpenAsync(TestContext.Current.CancellationToken);
            await lease.SaveAsync(Record(SessionPhase.Active), TestContext.Current.CancellationToken);
            var path = Path.Combine(directory.FullName, "session.bin");
            var corrupt = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
            corrupt[^1] ^= 1;
            await File.WriteAllBytesAsync(path, corrupt, TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<SessionStorageException>(() => lease.ReadAsync(TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<SessionStorageException>(() => lease.SaveAsync(Record(SessionPhase.Active), TestContext.Current.CancellationToken));
            Assert.Equal(corrupt, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact(Skip = "Requires Windows DPAPI.", SkipWhen = nameof(NotWindows))]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void WindowsAdapterUsesCurrentUserDpapiAndRejectsTampering()
    {
        if (!OperatingSystem.IsWindows()) { throw new InvalidOperationException("Windows-only test."); }
        var protector = new WindowsDeviceSecretProtector();
        var plaintext = System.Text.Encoding.UTF8.GetBytes("synthetic-lucia-device-credential");
        var encrypted = protector.Protect(plaintext);
        Assert.NotEqual(plaintext, encrypted);
        Assert.Equal(plaintext, protector.Unprotect(encrypted));
        encrypted[^1] ^= 1;
        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() => protector.Unprotect(encrypted));
    }

    [Fact(Skip = "Requires Windows DPAPI and Windows PowerShell.", SkipWhen = nameof(NotWindows))]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task WindowsVaultPersistsAcrossInstancesAndItsLeaseExcludesAnotherProcess()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-windows-vault-");
        try
        {
            var vault = new ProtectedSessionVault(directory.FullName, new WindowsDeviceSecretProtector());
            await using (var lease = await vault.OpenAsync(TestContext.Current.CancellationToken))
            {
                await lease.SaveAsync(Record(SessionPhase.Active), TestContext.Current.CancellationToken);
                var start = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
                    UseShellExecute = false, CreateNoWindow = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", Path.Combine(AppContext.BaseDirectory, "Probes", "CheckSessionLock.ps1"), "-LockPath", Path.Combine(directory.FullName, ".session.lock") })
                { start.ArgumentList.Add(argument); }
                using var probe = System.Diagnostics.Process.Start(start)!;
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
                deadline.CancelAfter(TimeSpan.FromSeconds(15));
                try { await probe.WaitForExitAsync(deadline.Token); Assert.Equal(23, probe.ExitCode); }
                finally { if (!probe.HasExited) { probe.Kill(); await probe.WaitForExitAsync(TestContext.Current.CancellationToken); } }
            }
            await using var reopened = await new ProtectedSessionVault(directory.FullName, new WindowsDeviceSecretProtector()).OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal("private-home", (await reopened.ReadAsync(TestContext.Current.CancellationToken))!.HomeId);
            await reopened.ClearAsync(TestContext.Current.CancellationToken);
        }
        finally { directory.Delete(recursive: true); }
    }

    public static bool NotWindows => !OperatingSystem.IsWindows();

    [Fact]
    public async Task ProtectionFailureAndCancellationLeavePreviousCiphertextUnchanged()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-vault-write-fault-");
        using var protector = new TestSecretProtector();
        try
        {
            await using var lease = await new ProtectedSessionVault(directory.FullName, protector).OpenAsync(TestContext.Current.CancellationToken);
            await lease.SaveAsync(Record(SessionPhase.Active), TestContext.Current.CancellationToken);
            var path = Path.Combine(directory.FullName, "session.bin");
            var original = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
            protector.FailOnProtect = protector.ProtectCalls + 1;
            await Assert.ThrowsAsync<SessionStorageException>(() => lease.SaveAsync(Record(SessionPhase.Renewing), TestContext.Current.CancellationToken));
            Assert.Equal(original, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lease.SaveAsync(Record(SessionPhase.Renewing), cancelled.Token));
            Assert.Equal(original, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            Assert.Empty(Directory.EnumerateFiles(directory.FullName, "*.tmp"));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task OversizedProtectedFileFailsClosedWithoutChangingIt()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-vault-oversized-");
        using var protector = new TestSecretProtector();
        try
        {
            var path = Path.Combine(directory.FullName, "session.bin");
            await File.WriteAllBytesAsync(path, new byte[16_385], TestContext.Current.CancellationToken);
            await using var lease = await new ProtectedSessionVault(directory.FullName, protector).OpenAsync(TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<SessionStorageException>(() => lease.ReadAsync(TestContext.Current.CancellationToken));
            Assert.Equal(16_385, new FileInfo(path).Length);
        }
        finally { directory.Delete(recursive: true); }
    }

    internal static StoredDeviceSession Record(SessionPhase phase, DeviceCredentials? credentials = null) => new(1, new Uri("https://localhost"),
        credentials ?? new(new string('a', 32), "lca_" + new string('a', 43), DateTimeOffset.UtcNow.AddMinutes(5), "lcr_" + new string('b', 43), DateTimeOffset.UtcNow.AddDays(7)),
        phase, phase == SessionPhase.Verifying ? null : "private-home", phase == SessionPhase.Verifying ? null : "member-a");
}

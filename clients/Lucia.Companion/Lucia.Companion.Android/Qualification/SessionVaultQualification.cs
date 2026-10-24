using Android.App;
using Android.OS;
using Android.Runtime;
using Java.Security;
using Lucia.Companion.Android.Security;
using Lucia.Companion.Connection;
using System.Security.Cryptography;

namespace Lucia.Companion.Android.Qualification;

/// <summary>Explicit Debug build only. Synthetic credentials, separate key alias and temporary no-backup directory.</summary>
[Instrumentation(Name = "org.lucia.companion.prototype.SessionVaultQualification", TargetPackage = "org.lucia.companion.prototype", FunctionalTest = true)]
public sealed class SessionVaultQualification : Instrumentation
{
    public SessionVaultQualification() { }
    public SessionVaultQualification(nint handle, JniHandleOwnership transfer) : base(handle, transfer) { }
    private readonly List<string> checks = [];
    private string currentCheck = "startup";
    private string stage = "all";
    private string? runId;

    public override void OnCreate(Bundle? arguments)
    {
        base.OnCreate(arguments);
        stage = arguments?.GetString("stage") ?? "all";
        runId = arguments?.GetString("runId");
        Start();
    }

    public override void OnStart()
    {
        base.OnStart();
        using var result = new Bundle();
        try
        {
            if (stage == "all") { RunAsync().GetAwaiter().GetResult(); }
            else { RunStageAsync().GetAwaiter().GetResult(); }
            result.PutString("status", "passed");
            result.PutInt("checks", checks.Count);
            result.PutString("evidence", string.Join(",", checks));
            Finish(Result.Ok, result);
        }
        catch (Exception error)
        {
            // No exception text/stack, ciphertext or credential is sent to adb/logcat.
            result.PutString("status", "failed");
            result.PutString("check", currentCheck);
            result.PutString("failureType", error.GetType().Name);
            Finish(Result.Canceled, result);
        }
    }

    private async Task RunAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = deadline.Token;
        var noBackup = TargetContext?.NoBackupFilesDir?.AbsolutePath ?? throw new InvalidOperationException("Missing private directory.");
        var directory = Path.Combine(noBackup, "vault-qualification-" + Guid.NewGuid().ToString("N"));
        var alias = "Lucia.Qualification." + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(directory);
        try
        {
            var protector = new AndroidDeviceSecretProtector(alias);
            var plaintext = "synthetic-device-credential"u8.ToArray();
            var encrypted = protector.Protect(plaintext);
            Check("randomized-encryption", !encrypted.SequenceEqual(protector.Protect(plaintext)));
            Check("key-reopen-roundtrip", plaintext.SequenceEqual(new AndroidDeviceSecretProtector(alias).Unprotect(encrypted)));
            using (var keys = KeyStore.GetInstance("AndroidKeyStore")!)
            {
                keys.Load(null);
                using var key = keys.GetKey(alias, null)!;
                Check("key-not-exportable", key.GetEncoded() is null);
            }
            currentCheck = "tamper-rejected";
            var corrupted = encrypted.ToArray(); corrupted[^1] ^= 1;
            ExpectCrypto(() => protector.Unprotect(corrupted));
            checks.Add(currentCheck);
            currentCheck = "version-and-truncation-rejected";
            var unknownVersion = encrypted.ToArray(); unknownVersion[0] = 99;
            ExpectCrypto(() => protector.Unprotect(unknownVersion));
            ExpectCrypto(() => protector.Unprotect(new byte[4]));
            checks.Add(currentCheck);

            var vault = new ProtectedSessionVault(directory, protector);
            var record = SyntheticRecord();
            await using (var lease = await vault.OpenAsync(token).ConfigureAwait(false))
            {
                await lease.SaveAsync(record, token).ConfigureAwait(false);
                currentCheck = "exclusive-vault-lease";
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(token);
                wait.CancelAfter(TimeSpan.FromMilliseconds(150));
                try { await using var other = await new ProtectedSessionVault(directory, protector).OpenAsync(wait.Token).ConfigureAwait(false); throw new InvalidOperationException("Lease was not exclusive."); }
                catch (System.OperationCanceledException) when (!token.IsCancellationRequested) { checks.Add(currentCheck); }
            }
            var path = Path.Combine(directory, "session.bin");
            var ciphertext = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
            Check("ciphertext-only-file", !System.Text.Encoding.UTF8.GetString(ciphertext).Contains("lcr_", StringComparison.Ordinal));
            await using (var lease = await new ProtectedSessionVault(directory, new AndroidDeviceSecretProtector(alias)).OpenAsync(token).ConfigureAwait(false))
            {
                var restored = await lease.ReadAsync(token).ConfigureAwait(false);
                Check("renewal-marker-restored", restored?.Phase == SessionPhase.Renewing && restored.HomeId == "synthetic-home");
                ciphertext[^1] ^= 1;
                await File.WriteAllBytesAsync(path, ciphertext, token).ConfigureAwait(false);
                currentCheck = "corrupt-file-preserved";
                try { await lease.ReadAsync(token).ConfigureAwait(false); throw new InvalidOperationException("Corruption was accepted."); }
                catch (SessionStorageException)
                {
                    var retained = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
                    Check(currentCheck, ciphertext.SequenceEqual(retained));
                }
                await lease.ClearAsync(token).ConfigureAwait(false);
                await lease.SaveAsync(record, token).ConfigureAwait(false);
            }
            DeleteKey(alias);
            currentCheck = "key-loss-fails-closed";
            await using (var lease = await vault.OpenAsync(token).ConfigureAwait(false))
            {
                try { await lease.ReadAsync(token).ConfigureAwait(false); throw new InvalidOperationException("Lost key was accepted."); }
                catch (SessionStorageException) { Check(currentCheck, File.Exists(path)); }
                ExpectCrypto(() => protector.Unprotect(encrypted));
                using var keys = KeyStore.GetInstance("AndroidKeyStore")!; keys.Load(null);
                Check("read-does-not-regenerate-key", !keys.ContainsAlias(alias));
                await lease.ClearAsync(token).ConfigureAwait(false);
                Check("explicit-forget-clears-file", !File.Exists(path));
            }
        }
        finally
        {
            Cleanup(noBackup, directory, alias);
        }
    }

    private async Task RunStageAsync()
    {
        if (stage is not ("seed" or "resume" or "cleanup") || !Guid.TryParseExact(runId, "N", out var id))
        { throw new InvalidOperationException("Use a supported synthetic stage and a GUID run identifier."); }
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = deadline.Token;
        var noBackup = TargetContext?.NoBackupFilesDir?.AbsolutePath ?? throw new InvalidOperationException("Missing private directory.");
        var directory = Path.Combine(noBackup, "vault-qualification-persist-" + id.ToString("N"));
        var alias = "Lucia.Qualification.Persistent." + id.ToString("N");
        if (stage == "cleanup") { Cleanup(noBackup, directory, alias); Check("explicit-synthetic-cleanup", !Directory.Exists(directory)); return; }
        var vault = new ProtectedSessionVault(directory, new AndroidDeviceSecretProtector(alias));
        if (stage == "seed")
        {
            if (Directory.Exists(directory)) { throw new InvalidOperationException("This synthetic run already exists."); }
            using var keys = KeyStore.GetInstance("AndroidKeyStore")!; keys.Load(null);
            if (keys.ContainsAlias(alias)) { throw new InvalidOperationException("This synthetic key already exists."); }
            await using var lease = await vault.OpenAsync(token).ConfigureAwait(false);
            await lease.SaveAsync(SyntheticRecord(), token).ConfigureAwait(false);
            Check("renewal-marker-protected-before-stop", File.Exists(Path.Combine(directory, "session.bin")));
            using var reopened = KeyStore.GetInstance("AndroidKeyStore")!; reopened.Load(null);
            Check("seed-key-present", reopened.ContainsAlias(alias));
            // Intentionally leave this run's ciphertext/key until a fresh process executes resume or explicit cleanup.
            return;
        }
        try
        {
            using var keys = KeyStore.GetInstance("AndroidKeyStore")!; keys.Load(null);
            Check("key-persisted-across-process", keys.ContainsAlias(alias));
            await using var lease = await vault.OpenAsync(token).ConfigureAwait(false);
            var restored = await lease.ReadAsync(token).ConfigureAwait(false);
            Check("renewal-marker-after-process-stop", restored?.Phase == SessionPhase.Renewing && restored.HomeId == "synthetic-home"
                && restored.Credentials.SessionId == new string('a', 32));
            await lease.ClearAsync(token).ConfigureAwait(false);
            Check("cold-process-forget", !File.Exists(Path.Combine(directory, "session.bin")));
        }
        finally { Cleanup(noBackup, directory, alias); }
    }

    private static StoredDeviceSession SyntheticRecord() => new(1, new Uri("https://synthetic.invalid"), new(new string('a', 32), "lca_" + new string('a', 43),
        DateTimeOffset.UtcNow.AddMinutes(5), "lcr_" + new string('b', 43), DateTimeOffset.UtcNow.AddDays(7)), SessionPhase.Renewing, "synthetic-home", "synthetic-member");

    private static void Cleanup(string noBackup, string directory, string alias)
    {
        // Exact synthetic run directory inside this app's no-backup folder, never the production vault or a parent folder.
        var fullDirectory = Path.GetFullPath(directory);
        if (!fullDirectory.StartsWith(Path.GetFullPath(noBackup) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        { throw new InvalidOperationException("Refusing unsafe qualification cleanup."); }
        DeleteKey(alias);
        if (Directory.Exists(fullDirectory)) { Directory.Delete(fullDirectory, recursive: true); }
    }

    private void Check(string name, bool condition)
    {
        currentCheck = name;
        if (!condition) { throw new InvalidOperationException("Qualification check failed."); }
        checks.Add(name);
    }
    private static void ExpectCrypto(Action action)
    {
        try { action(); }
        catch (CryptographicException) { return; }
        throw new InvalidOperationException("Expected authenticated decryption failure.");
    }
    private static void DeleteKey(string alias)
    {
        using var keys = KeyStore.GetInstance("AndroidKeyStore")!; keys.Load(null); keys.DeleteEntry(alias);
    }
}

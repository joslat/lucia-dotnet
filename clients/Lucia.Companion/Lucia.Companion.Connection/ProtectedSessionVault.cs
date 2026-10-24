using System.Diagnostics;

namespace Lucia.Companion.Connection;

/// <summary>Only ciphertext on disk. A separate lock file is never replaced while writers wait.</summary>
public sealed class ProtectedSessionVault(string directory, IDeviceSecretProtector protector) : ISessionVault
{
    private readonly string directory = Path.GetFullPath(directory);

    public async Task<ISessionVaultLease> OpenAsync(CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            Directory.CreateDirectory(directory);
            if (File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint)) { throw new SessionStorageException(); }
            var lockPath = Path.Combine(directory, ".session.lock");
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (File.Exists(lockPath) && File.GetAttributes(lockPath).HasFlag(FileAttributes.ReparsePoint)) { throw new SessionStorageException(); }
                    var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.Asynchronous);
                    return new FileSessionVaultLease(Path.Combine(directory, "session.bin"), protector, stream);
                }
                catch (IOException) when (Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(75))
                { await Task.Delay(50, cancellationToken).ConfigureAwait(false); }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { throw new SessionStorageException(); }
    }
}

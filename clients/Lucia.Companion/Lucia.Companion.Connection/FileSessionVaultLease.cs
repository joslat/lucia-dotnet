using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lucia.Companion.Connection;

internal sealed class FileSessionVaultLease(string path, IDeviceSecretProtector protector, FileStream fileLock) : ISessionVaultLease
{
    private const int MaximumBytes = 16_384;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter<SessionPhase>(allowIntegerValues: false) }
    };
    private bool disposed;

    public async Task<StoredDeviceSession?> ReadAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        byte[]? plaintext = null;
        try
        {
            ValidateFile();
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            if (stream.Length is <= 0 or > MaximumBytes) { throw new SessionStorageException(); }
            var ciphertext = new byte[stream.Length];
            await stream.ReadExactlyAsync(ciphertext, cancellationToken).ConfigureAwait(false);
            plaintext = protector.Unprotect(ciphertext);
            if (plaintext.Length is <= 0 or > MaximumBytes) { throw new SessionStorageException(); }
            return JsonSerializer.Deserialize<StoredDeviceSession>(plaintext, Options) ?? throw new SessionStorageException();
        }
        catch (FileNotFoundException) { return null; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException or JsonException or ArgumentException)
        { throw new SessionStorageException(); }
        finally { if (plaintext is not null) { CryptographicOperations.ZeroMemory(plaintext); } }
    }

    public async Task SaveAsync(StoredDeviceSession session, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(session);
        // A caller cannot silently replace an unreadable or newer-schema connection. Clear is an explicit separate choice.
        await ReadAsync(cancellationToken).ConfigureAwait(false);
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(session, Options);
        string? temporary = null;
        try
        {
            if (File.Exists(path)) { ValidateFile(); }
            var ciphertext = protector.Protect(plaintext);
            if (ciphertext.Length is <= 0 or > MaximumBytes) { throw new SessionStorageException(); }
            temporary = Path.Combine(Path.GetDirectoryName(path)!, $".session-{Guid.NewGuid():N}.tmp");
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(ciphertext, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException or ArgumentException)
        { throw new SessionStorageException(); }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (temporary is not null)
            {
                try { File.Delete(temporary); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { /* Ciphertext only; an interrupted cleanup cannot justify a plaintext fallback. */ }
            }
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (File.Exists(path)) { ValidateFile(); File.Delete(path); }
            return Task.CompletedTask;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { throw new SessionStorageException(); }
    }

    private void ValidateFile()
    {
        if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) { throw new SessionStorageException(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (!disposed) { disposed = true; await fileLock.DisposeAsync().ConfigureAwait(false); }
    }
}

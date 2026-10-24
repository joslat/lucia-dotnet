namespace Lucia.Companion.Connection;

/// <summary>Exclusive across cooperating processes, including credential-changing network requests.</summary>
public interface ISessionVaultLease : IAsyncDisposable
{
    Task<StoredDeviceSession?> ReadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(StoredDeviceSession session, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

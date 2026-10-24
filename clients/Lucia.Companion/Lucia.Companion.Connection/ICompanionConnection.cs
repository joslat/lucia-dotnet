namespace Lucia.Companion.Connection;

/// <summary>UI-safe operations. No access, refresh or pending-device secret crosses this boundary.</summary>
public interface ICompanionConnection
{
    Uri? Endpoint { get; }
    bool HasSavedConnection { get; }
    Task<ConnectionSession?> RestoreAsync(CancellationToken cancellationToken = default);
    Task<PairingPrompt> BeginAsync(Uri endpoint, string deviceName, CancellationToken cancellationToken = default);
    Task<PairingUpdate> PollAsync(CancellationToken cancellationToken = default);
    Task<bool> DisconnectAsync(CancellationToken cancellationToken = default);
    Task ForgetAsync(CancellationToken cancellationToken = default);
    Task<HomeSnapshot> ReadHomeAsync(CancellationToken cancellationToken = default);
    Task<HomeSnapshot> ReadHomeAsync(ConnectionSession expected, CancellationToken cancellationToken = default);
    Task<HomeUpdate> ReadHomeUpdateAsync(ConnectionSession expected, string? revision, CancellationToken cancellationToken = default);
    Task<ActivitySnapshot> ReadActivityAsync(ConnectionSession expected, CancellationToken cancellationToken = default);
    Task<HomeCatalog> DiscoverAsync(CancellationToken cancellationToken = default);
    Task<SharedHomeLayout?> ReadSharedLayoutAsync(ConnectionSession expected, CancellationToken cancellationToken = default);
}

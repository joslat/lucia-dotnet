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
}

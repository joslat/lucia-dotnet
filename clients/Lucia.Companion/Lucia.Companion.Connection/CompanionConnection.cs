namespace Lucia.Companion.Connection;

/// <summary>Application lifetime orchestration around one protected connection and one in-memory pairing challenge.</summary>
public sealed class CompanionConnection : ICompanionConnection, IDisposable
{
    private readonly ISessionVault vault;
    private readonly Func<Uri, CompanionApiClient> createClient;
    private readonly TimeProvider clock;
    private readonly SemaphoreSlim gate = new(1, 1);
    private CompanionApiClient? client;
    private DevicePairingChallenge? pending;
    private bool disposed;
    public Uri? Endpoint { get; private set; }
    public bool HasSavedConnection { get; private set; }

    public CompanionConnection(ISessionVault vault) : this(vault, endpoint => new CompanionApiClient(endpoint)) { }
    internal CompanionConnection(ISessionVault vault, Func<Uri, CompanionApiClient> createClient, TimeProvider? clock = null)
    { this.vault = vault; this.createClient = createClient; this.clock = clock ?? TimeProvider.System; }

    public async Task<ConnectionSession?> RestoreAsync(CancellationToken cancellationToken = default)
    {
        await EnterAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            StoredDeviceSession? stored;
            await using (var lease = await vault.OpenAsync(cancellationToken).ConfigureAwait(false))
            { stored = await lease.ReadAsync(cancellationToken).ConfigureAwait(false); }
            HasSavedConnection = stored is not null;
            if (stored is null) { return null; }
            var api = ClientFor(stored.Endpoint);
            try
            {
                var session = await new DeviceSessionCoordinator(api, vault, clock).RestoreAsync(cancellationToken).ConfigureAwait(false);
                HasSavedConnection = session is not null;
                return session is null ? null : new(api.Endpoint, session);
            }
            catch (CompanionConnectionException error) when (error.Failure == ConnectionFailure.Unauthorized || error.StatusCode == System.Net.HttpStatusCode.Forbidden)
            { HasSavedConnection = false; throw; }
            catch (SessionStateException error) when (error.Failure == SessionStateFailure.ConnectionRequired)
            { HasSavedConnection = false; throw; }
            catch (SessionStateException error) when (error.Failure == SessionStateFailure.IdentityChanged)
            { HasSavedConnection = true; throw; } // A failed rotation may still have its durable Renewing marker.
        }
        catch (SessionStorageException) { HasSavedConnection = true; throw; }
        finally { gate.Release(); }
    }

    public async Task<PairingPrompt> BeginAsync(Uri endpoint, string deviceName, CancellationToken cancellationToken = default)
    {
        CompanionEndpoint.Validate(endpoint);
        await EnterAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using (var lease = await vault.OpenAsync(cancellationToken).ConfigureAwait(false))
            { HasSavedConnection = await lease.ReadAsync(cancellationToken).ConfigureAwait(false) is not null; }
            if (HasSavedConnection) { throw new SessionStateException(SessionStateFailure.AlreadyConnected); }
            pending = null;
            pending = await ClientFor(endpoint).StartPairingAsync(deviceName, cancellationToken).ConfigureAwait(false);
            return new(pending.UserCode, pending.ExpiresAt, pending.PollIntervalSeconds);
        }
        catch (SessionStorageException) { HasSavedConnection = true; throw; }
        finally { gate.Release(); }
    }

    public async Task<PairingUpdate> PollAsync(CancellationToken cancellationToken = default)
    {
        await EnterAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (pending is null || client is null) { throw new SessionStateException(SessionStateFailure.ConnectionRequired); }
            if (pending.ExpiresAt <= clock.GetUtcNow()) { pending = null; return new(PairingPollStatus.Expired, TimeSpan.Zero); }
            var result = await client.PollPairingAsync(pending.DeviceCode, cancellationToken).ConfigureAwait(false);
            if (result.Status != PairingPollStatus.Success)
            {
                if (result.Status is not (PairingPollStatus.Pending or PairingPollStatus.SlowDown)) { pending = null; }
                return new(result.Status, result.RetryAfter);
            }
            pending = null;
            HasSavedConnection = true;
            // Once credentials arrive, navigation cancellation must not discard them before protection.
            using var commit = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var session = await new DeviceSessionCoordinator(client, vault, clock).ConnectAsync(result.Credentials!, commit.Token).ConfigureAwait(false);
            return new(PairingPollStatus.Success, TimeSpan.Zero, new(client.Endpoint, session));
        }
        catch { pending = null; throw; }
        finally { gate.Release(); }
    }

    public async Task<bool> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await EnterAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            StoredDeviceSession? stored;
            await using (var lease = await vault.OpenAsync(cancellationToken).ConfigureAwait(false))
            { stored = await lease.ReadAsync(cancellationToken).ConfigureAwait(false); }
            pending = null;
            if (stored is null) { HasSavedConnection = false; return true; }
            var confirmed = await new DeviceSessionCoordinator(ClientFor(stored.Endpoint), vault, clock).DisconnectAsync(cancellationToken).ConfigureAwait(false);
            HasSavedConnection = false;
            return confirmed;
        }
        finally { gate.Release(); }
    }

    public async Task ForgetAsync(CancellationToken cancellationToken = default)
    {
        await EnterAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var lease = await vault.OpenAsync(cancellationToken).ConfigureAwait(false);
            await lease.ClearAsync(cancellationToken).ConfigureAwait(false);
            pending = null; HasSavedConnection = false;
        }
        finally { gate.Release(); }
    }

    private CompanionApiClient ClientFor(Uri endpoint)
    {
        if (client is null || client.Endpoint != endpoint) { client?.Dispose(); client = createClient(endpoint); }
        Endpoint = client.Endpoint;
        return client;
    }
    private async Task EnterAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (disposed) { gate.Release(); throw new ObjectDisposedException(nameof(CompanionConnection)); }
    }
    public void Dispose() { disposed = true; client?.Dispose(); }
}

using System.Net;

namespace Lucia.Companion.Connection;

/// <summary>Holds one exclusive vault lease through read/mark/renew/save. No cached permission is returned offline.</summary>
public sealed class DeviceSessionCoordinator(CompanionApiClient api, ISessionVault vault, TimeProvider? clock = null)
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;

    public async Task<DeviceSession> ConnectAsync(DeviceCredentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        await using var lease = await vault.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (await lease.ReadAsync(cancellationToken).ConfigureAwait(false) is not null)
        { throw new SessionStateException(SessionStateFailure.AlreadyConnected); }
        if (credentials.AccessExpiresAt <= clock.GetUtcNow() || credentials.RefreshExpiresAt <= clock.GetUtcNow())
        { throw new SessionStateException(SessionStateFailure.ConnectionRequired); }
        var pending = new StoredDeviceSession(1, api.Endpoint, credentials, SessionPhase.Verifying, null, null);
        // Redemption has already committed. Protect the received credentials before a potentially interrupted grant read.
        await lease.SaveAsync(pending, cancellationToken).ConfigureAwait(false);
        return await VerifyAsync(lease, pending, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeviceSession?> RestoreAsync(CancellationToken cancellationToken = default)
    {
        await using var lease = await vault.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await RestoreCoreAsync(lease, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DeviceSession?> RestoreCoreAsync(ISessionVaultLease lease, CancellationToken cancellationToken)
    {
        var stored = await lease.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (stored is null) { return null; }
        RequireEndpoint(stored);
        if (stored.Phase == SessionPhase.Renewing)
        {
            // The server may have rotated even if the client never saw the reply. Never replay this refresh.
            await lease.ClearAsync(cancellationToken).ConfigureAwait(false);
            await TryRevokeAsync(stored.Credentials.RefreshToken, cancellationToken).ConfigureAwait(false);
            throw new SessionStateException(SessionStateFailure.ConnectionRequired);
        }
        if (stored.Credentials.RefreshExpiresAt <= clock.GetUtcNow())
        {
            await lease.ClearAsync(cancellationToken).ConfigureAwait(false);
            throw new SessionStateException(SessionStateFailure.ConnectionRequired);
        }
        if (stored.Credentials.AccessExpiresAt <= clock.GetUtcNow().AddSeconds(30))
        {
            await lease.SaveAsync(new(1, stored.Endpoint, stored.Credentials, SessionPhase.Renewing, stored.HomeId, stored.MemberId), cancellationToken).ConfigureAwait(false);
            var renewed = await api.RenewAsync(stored.Credentials.RefreshToken, cancellationToken).ConfigureAwait(false);
            if (renewed.SessionId != stored.Credentials.SessionId || renewed.AccessToken == stored.Credentials.AccessToken
                || renewed.RefreshToken == stored.Credentials.RefreshToken || renewed.AccessExpiresAt <= clock.GetUtcNow()
                || renewed.RefreshExpiresAt <= clock.GetUtcNow())
            { throw new SessionStateException(SessionStateFailure.IdentityChanged); }
            stored = new(1, stored.Endpoint, renewed, stored.HomeId is null ? SessionPhase.Verifying : SessionPhase.Active, stored.HomeId, stored.MemberId);
            // Do not issue an authenticated read until the replacement is durably protected.
            await lease.SaveAsync(stored, cancellationToken).ConfigureAwait(false);
        }
        return await VerifyAsync(lease, stored, cancellationToken).ConfigureAwait(false);
    }

    public Task<HomeSnapshot> ReadHomeAsync(CancellationToken cancellationToken = default) => ReadAuthorizedAsync(api.GetHomeAsync, cancellationToken);
    public Task<HomeSnapshot> ReadHomeAsync(ConnectionSession expected, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        return ReadAuthorizedAsync(api.GetHomeAsync, cancellationToken, expected);
    }
    public Task<HomeCatalog> DiscoverAsync(CancellationToken cancellationToken = default) => ReadAuthorizedAsync(api.GetDiscoveryAsync, cancellationToken);
    public Task<HomeUpdate> ReadHomeUpdateAsync(ConnectionSession expected, string? revision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        return ReadAuthorizedAsync((access, session, ct) => api.GetHomeUpdateAsync(access, session, revision, ct), cancellationToken, expected);
    }
    public Task<ActivitySnapshot> ReadActivityAsync(ConnectionSession expected, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (!expected.Session.CanReadActivity) { throw new ArgumentException("Activity requires its explicit grant.", nameof(expected)); }
        return ReadAuthorizedAsync((access, current, ct) => current.CanReadActivity ? api.GetActivityAsync(access, current, ct)
            : throw new SessionStateException(SessionStateFailure.IdentityChanged), cancellationToken, expected);
    }

    private async Task<T> ReadAuthorizedAsync<T>(Func<string, DeviceSession, CancellationToken, Task<T>> read, CancellationToken cancellationToken, ConnectionSession? expected = null)
    {
        // Keep renewal, verification and data read under the same cross-instance lease. No token is returned to the app.
        await using var lease = await vault.OpenAsync(cancellationToken).ConfigureAwait(false);
        var metadata = await RestoreCoreAsync(lease, cancellationToken).ConfigureAwait(false)
            ?? throw new SessionStateException(SessionStateFailure.ConnectionRequired);
        if (expected is not null && (expected.Endpoint != api.Endpoint || !MatchesHomeAccess(expected.Session, metadata)))
        { throw new SessionStateException(SessionStateFailure.IdentityChanged); }
        var stored = await lease.ReadAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new SessionStateException(SessionStateFailure.ConnectionRequired);
        try { return await read(stored.Credentials.AccessToken, metadata, cancellationToken).ConfigureAwait(false); }
        catch (CompanionConnectionException error) when (error.Failure == ConnectionFailure.Unauthorized || error.StatusCode == HttpStatusCode.Forbidden)
        {
            await lease.ClearAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static bool MatchesHomeAccess(DeviceSession expected, DeviceSession current) => expected.SessionId == current.SessionId
        && expected.HomeId == current.HomeId && expected.MemberId == current.MemberId && expected.Places.Count == current.Places.Count
        && current.Places.All(place => expected.Places.SingleOrDefault(item => item.PlaceId == place.PlaceId) is { } previous
            && previous.Fields.ToHashSet().SetEquals(place.Fields));

    /// <returns>True only when the host confirmed revocation, or there was no stored connection. False means local removal only.</returns>
    public async Task<bool> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await using var lease = await vault.OpenAsync(cancellationToken).ConfigureAwait(false);
        var stored = await lease.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (stored is null) { return true; }
        RequireEndpoint(stored);
        await lease.ClearAsync(cancellationToken).ConfigureAwait(false);
        return await TryRevokeAsync(stored.Credentials.RefreshToken, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Explicit local-only recovery for unreadable or foreign-host storage. Does not claim server revocation.</summary>
    public async Task ForgetAsync(CancellationToken cancellationToken = default)
    {
        await using var lease = await vault.OpenAsync(cancellationToken).ConfigureAwait(false);
        await lease.ClearAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<DeviceSession> VerifyAsync(ISessionVaultLease lease, StoredDeviceSession stored, CancellationToken cancellationToken)
    {
        DeviceSession metadata;
        try { metadata = await api.GetSessionAsync(stored.Credentials.AccessToken, cancellationToken).ConfigureAwait(false); }
        catch (CompanionConnectionException error) when (error.Failure == ConnectionFailure.Unauthorized || error.StatusCode == HttpStatusCode.Forbidden)
        {
            await lease.ClearAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
        if (metadata.SessionId != stored.Credentials.SessionId || metadata.ExpiresAt <= clock.GetUtcNow()
            || (stored.HomeId is not null && (metadata.HomeId != stored.HomeId || metadata.MemberId != stored.MemberId)))
        {
            await lease.ClearAsync(cancellationToken).ConfigureAwait(false);
            await TryRevokeAsync(stored.Credentials.RefreshToken, cancellationToken).ConfigureAwait(false);
            throw new SessionStateException(SessionStateFailure.IdentityChanged);
        }
        if (stored.Phase == SessionPhase.Verifying)
        {
            await lease.SaveAsync(new(1, stored.Endpoint, stored.Credentials, SessionPhase.Active, metadata.HomeId, metadata.MemberId), cancellationToken).ConfigureAwait(false);
        }
        return metadata;
    }

    private void RequireEndpoint(StoredDeviceSession stored)
    { if (stored.Endpoint != api.Endpoint) { throw new SessionStateException(SessionStateFailure.WrongEndpoint); } }

    private async Task<bool> TryRevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        try { await api.RevokeAsync(refreshToken, deadline.Token).ConfigureAwait(false); return true; }
        catch (Exception error) when (error is CompanionConnectionException or OperationCanceledException) { return false; }
    }
}

using System.Data.Common;
using System.Security.Cryptography;
using lucia.Agents.Companion;
using static lucia.Data.Companion.CompanionSql;

namespace lucia.Data.Companion;

public sealed class RelationalCompanionPairingService : ICompanionPairingService
{
    private const string DevicePrefix = "lcp_";
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private readonly Func<CancellationToken, ValueTask<DbConnection>> openConnection;
    private readonly CompanionOptions options;
    private readonly TimeProvider clock;

    public RelationalCompanionPairingService(Func<CancellationToken, ValueTask<DbConnection>> openConnection, CompanionOptions options, TimeProvider clock)
    {
        options.Validate(); this.openConnection = openConnection; this.options = options; this.clock = clock;
    }

    public async Task<CompanionPairingChallenge?> StartAsync(string deviceName, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled) { throw new InvalidOperationException("Companion access is disabled."); }
        deviceName = CompanionDeviceName.Validate(deviceName);
        var now = clock.GetUtcNow();
        await using var connection = await openConnection(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        // Lock one existing home row before counting; separate PostgreSQL processes cannot overbook the anonymous intent quota.
        await using (var gate = Command(connection, transaction,
            "INSERT INTO companion_pairing_gates(home_id, revision) VALUES (@home, 0) ON CONFLICT(home_id) DO NOTHING;", ("home", options.HomeId)))
        { await gate.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        await using (var gate = Command(connection, transaction, "UPDATE companion_pairing_gates SET revision = revision + 1 WHERE home_id = @home;", ("home", options.HomeId)))
        { await gate.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        await using (var prune = Command(connection, transaction, """
            DELETE FROM companion_pairings WHERE id IN
              (SELECT id FROM companion_pairings WHERE home_id = @home AND expires_at <= @now ORDER BY expires_at LIMIT 500);
            """, ("home", options.HomeId), ("now", now.ToUnixTimeMilliseconds())))
        { await prune.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        await using (var count = Command(connection, transaction,
            "SELECT COUNT(*) FROM companion_pairings WHERE home_id = @home AND expires_at > @now AND status IN ('Pending', 'Approved');",
            ("home", options.HomeId), ("now", now.ToUnixTimeMilliseconds())))
        {
            if (Convert.ToInt64(await count.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) >= options.MaximumPendingPairings) { return null; }
        }
        var secret = CompanionTokens.Create(DevicePrefix);
        var code = new string(RandomNumberGenerator.GetBytes(10).Select(value => Alphabet[value & 31]).ToArray());
        var expiry = now + options.PairingLifetime;
        await using (var insert = Command(connection, transaction, """
            INSERT INTO companion_pairings(id, home_id, device_name, device_hash, user_hash, expires_at, next_poll_at, status)
            VALUES (@id, @home, @name, @device, @user, @expiry, @now, 'Pending');
            """, ("id", Guid.NewGuid().ToString("N")), ("home", options.HomeId), ("name", deviceName),
            ("device", CompanionTokens.Hash(secret)), ("user", UserHash(code)), ("expiry", expiry.ToUnixTimeMilliseconds()), ("now", now.ToUnixTimeMilliseconds())))
        { await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(secret, code[..5] + "-" + code[5..], expiry);
    }

    public async Task<CompanionPairingReview?> ReviewAsync(string userCode, CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(userCode);
        if (!options.Enabled || code is null) { return null; }
        await using var connection = await openConnection(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, null, """
            SELECT id, device_name, expires_at, status FROM companion_pairings
            WHERE home_id = @home AND user_hash = @hash AND expires_at > @now AND status IN ('Pending', 'Approved');
            """, ("home", options.HomeId), ("hash", UserHash(code)), ("now", clock.GetUtcNow().ToUnixTimeMilliseconds()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new(reader.GetString(0), options.HomeId, reader.GetString(1), DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(2)), reader.GetString(3)) : null;
    }

    public async Task<bool> ApproveAsync(string pairingId, CompanionGrant grant, string approverId, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled) { return false; }
        ArgumentNullException.ThrowIfNull(grant);
        if (grant.HomeId != options.HomeId) { throw new ArgumentException("The approval belongs to another home.", nameof(grant)); }
        CompanionIdentifier.Validate(approverId);
        await using var connection = await openConnection(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, null, """
            UPDATE companion_pairings SET status = 'Approved', grant_json = @grant, approver_id = @owner
            WHERE id = @id AND home_id = @home AND status = 'Pending' AND expires_at > @now;
            """, ("grant", CompanionGrantJson.Serialize(grant)), ("owner", approverId), ("id", pairingId), ("home", options.HomeId), ("now", clock.GetUtcNow().ToUnixTimeMilliseconds()));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async Task<bool> CancelAsync(string pairingId, CancellationToken cancellationToken = default)
    {
        await using var connection = await openConnection(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, null, """
            UPDATE companion_pairings SET status = 'Denied', grant_json = NULL
            WHERE id = @id AND home_id = @home AND status IN ('Pending', 'Approved');
            """, ("id", pairingId), ("home", options.HomeId));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async Task<CompanionPairingResult> RedeemAsync(string deviceCode, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled || !CompanionTokens.HasFormat(deviceCode, DevicePrefix)) { return new(CompanionPairingStatus.Invalid); }
        var now = clock.GetUtcNow().ToUnixTimeMilliseconds();
        var hash = CompanionTokens.Hash(deviceCode);
        await using var connection = await openConnection(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        string? id = null, name = null, state = null, grant = null;
        await using (var claim = Command(connection, transaction, """
            UPDATE companion_pairings SET next_poll_at = @next
            WHERE home_id = @home AND device_hash = @hash AND expires_at > @now AND next_poll_at <= @now AND status IN ('Pending', 'Approved')
            RETURNING id, device_name, status, grant_json;
            """, ("next", now + 5000), ("home", options.HomeId), ("hash", hash), ("now", now)))
        await using (var reader = await claim.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                id = reader.GetString(0); name = reader.GetString(1); state = reader.GetString(2); grant = reader.IsDBNull(3) ? null : reader.GetString(3);
            }
        }
        CompanionPairingResult result;
        if (state == "Approved")
        {
            var issued = await CompanionSessionIssuer.IssueAsync(connection, transaction, options, DateTimeOffset.FromUnixTimeMilliseconds(now),
                CompanionGrantJson.Deserialize(grant ?? throw new InvalidDataException("The approval grant is missing.")), name!, cancellationToken).ConfigureAwait(false);
            await using var consume = Command(connection, transaction, "UPDATE companion_pairings SET status = 'Consumed', grant_json = NULL WHERE id = @id;", ("id", id!));
            await consume.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            result = new(CompanionPairingStatus.Success, issued);
        }
        else if (state == "Pending") { result = new(CompanionPairingStatus.Pending); }
        else
        {
            await using var query = Command(connection, transaction,
                "SELECT status, expires_at FROM companion_pairings WHERE home_id = @home AND device_hash = @hash;", ("home", options.HomeId), ("hash", hash));
            await using var reader = await query.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            result = !await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? new(CompanionPairingStatus.Invalid)
                : reader.GetInt64(1) <= now ? new(CompanionPairingStatus.Expired)
                : reader.GetString(0) == "Consumed" ? new(CompanionPairingStatus.Consumed)
                : reader.GetString(0) == "Denied" ? new(CompanionPairingStatus.Denied) : new(CompanionPairingStatus.SlowDown);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<int> PruneExpiredAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await openConnection(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, null, """
            DELETE FROM companion_pairings WHERE id IN
              (SELECT id FROM companion_pairings WHERE expires_at <= @now ORDER BY expires_at LIMIT 500);
            """, ("now", clock.GetUtcNow().ToUnixTimeMilliseconds()));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string UserHash(string code) => CompanionTokens.Hash("companion-user-code:" + code);
    private static string? NormalizeCode(string code)
    {
        if (code is null || code.Length > 20) { return null; }
        var normalized = code.Trim().Replace("-", "", StringComparison.Ordinal).ToUpperInvariant();
        return normalized.Length == 10 && normalized.All(Alphabet.Contains) ? normalized : null;
    }
}

using System.Data.Common;
using lucia.Agents.Companion;
using static lucia.Data.Companion.CompanionSql;

namespace lucia.Data.Companion;

/// <summary>Shared issuance supports atomic pairing consumption and session insertion in one transaction.</summary>
internal static class CompanionSessionIssuer
{
    public static async Task<CompanionSessionTokens> IssueAsync(DbConnection connection, DbTransaction? transaction,
        CompanionOptions options, DateTimeOffset now, CompanionGrant grant, string deviceName, CancellationToken cancellationToken)
    {
        if (!options.Enabled) { throw new InvalidOperationException("Companion access is disabled."); }
        ArgumentNullException.ThrowIfNull(grant);
        if (grant.HomeId != options.HomeId) { throw new ArgumentException("The grant belongs to another home.", nameof(grant)); }
        deviceName = CompanionDeviceName.Validate(deviceName);
        var id = Guid.NewGuid().ToString("N");
        var expiresAt = now + options.SessionLifetime;
        var issued = new CompanionSessionTokens(id,
            CompanionTokens.Create(CompanionTokens.AccessPrefix), now + options.AccessLifetime < expiresAt ? now + options.AccessLifetime : expiresAt,
            CompanionTokens.Create(CompanionTokens.RefreshPrefix), now + options.RefreshLifetime < expiresAt ? now + options.RefreshLifetime : expiresAt);
        await using var command = Command(connection, transaction, """
            INSERT INTO companion_sessions(id, home_id, device_name, grant_json, access_hash, access_expires_at, refresh_hash, refresh_expires_at, expires_at)
            VALUES (@id, @home, @name, @grant, @access, @accessExpiry, @refresh, @refreshExpiry, @expiry);
            """, ("id", id), ("home", grant.HomeId), ("name", deviceName), ("grant", CompanionGrantJson.Serialize(grant)),
            ("access", CompanionTokens.Hash(issued.AccessToken)), ("accessExpiry", issued.AccessExpiresAt.ToUnixTimeMilliseconds()),
            ("refresh", CompanionTokens.Hash(issued.RefreshToken)), ("refreshExpiry", issued.RefreshExpiresAt.ToUnixTimeMilliseconds()),
            ("expiry", expiresAt.ToUnixTimeMilliseconds()));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return issued;
    }
}

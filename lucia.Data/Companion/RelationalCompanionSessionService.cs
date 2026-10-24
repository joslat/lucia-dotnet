using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using lucia.Agents.Companion;

namespace lucia.Data.Companion;

/// <summary>Durable device credentials. Shared SQL keeps both supported providers on the same rotation/revocation rules.</summary>
public sealed class RelationalCompanionSessionService : ICompanionSessionService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter<CompanionField>(allowIntegerValues: false) }
    };
    private readonly Func<CancellationToken, ValueTask<DbConnection>> openConnection;
    private readonly CompanionOptions options;
    private readonly TimeProvider clock;

    public RelationalCompanionSessionService(Func<CancellationToken, ValueTask<DbConnection>> openConnection,
        CompanionOptions options, TimeProvider clock)
    {
        options.Validate();
        this.openConnection = openConnection;
        this.options = options;
        this.clock = clock;
    }

    public async Task<CompanionSessionTokens> CreateAsync(CompanionGrant grant, string deviceName, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled) { throw new InvalidOperationException("Companion access is disabled."); }
        ArgumentNullException.ThrowIfNull(grant);
        if (grant.HomeId != options.HomeId) { throw new ArgumentException("The grant belongs to another home.", nameof(grant)); }
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        if (deviceName.Length > 80 || deviceName.Any(char.IsControl)) { throw new ArgumentException("A device name supports at most 80 printable characters.", nameof(deviceName)); }
        var now = clock.GetUtcNow();
        var id = Guid.NewGuid().ToString("N");
        var issued = NewTokens(id, now, now + options.SessionLifetime);
        await using var connection = await openConnection(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, null, """
            INSERT INTO companion_sessions(id, home_id, device_name, grant_json, access_hash, access_expires_at, refresh_hash, refresh_expires_at, expires_at)
            VALUES (@id, @home, @name, @grant, @access, @accessExpiry, @refresh, @refreshExpiry, @expiry);
            """, ("id", id), ("home", grant.HomeId), ("name", deviceName), ("grant", JsonSerializer.Serialize(grant, Json)),
            ("access", CompanionTokens.Hash(issued.AccessToken)), ("accessExpiry", issued.AccessExpiresAt.ToUnixTimeMilliseconds()),
            ("refresh", CompanionTokens.Hash(issued.RefreshToken)), ("refreshExpiry", issued.RefreshExpiresAt.ToUnixTimeMilliseconds()),
            ("expiry", (now + options.SessionLifetime).ToUnixTimeMilliseconds()));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return issued;
    }

    public async Task<CompanionSession?> AuthenticateAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled || !CompanionTokens.HasFormat(accessToken, CompanionTokens.AccessPrefix)) { return null; }
        await using var connection = await openConnection(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, null, """
            SELECT id, device_name, grant_json, expires_at FROM companion_sessions
            WHERE home_id = @home AND access_hash = @hash AND access_expires_at > @now AND expires_at > @now AND revoked_at IS NULL;
            """, ("home", options.HomeId), ("hash", CompanionTokens.Hash(accessToken)), ("now", clock.GetUtcNow().ToUnixTimeMilliseconds()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) { return null; }
        var grant = JsonSerializer.Deserialize<CompanionGrant>(reader.GetString(2), Json) ?? throw new InvalidDataException("The saved companion grant is missing.");
        if (grant.HomeId != options.HomeId) { return null; }
        return new(reader.GetString(0), reader.GetString(1), grant, DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(3)));
    }

    public async Task<CompanionSessionTokens?> RenewAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled || !CompanionTokens.HasFormat(refreshToken, CompanionTokens.RefreshPrefix)) { return null; }
        var now = clock.GetUtcNow();
        var access = CompanionTokens.Create(CompanionTokens.AccessPrefix);
        var refresh = CompanionTokens.Create(CompanionTokens.RefreshPrefix);
        var hash = CompanionTokens.Hash(refreshToken);
        await using var connection = await openConnection(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        string? id = null;
        long accessExpiry = 0, refreshExpiry = 0, expiry = 0;
        await using (var command = Command(connection, transaction, """
            UPDATE companion_sessions SET access_hash = @access, refresh_hash = @refresh,
              access_expires_at = CASE WHEN expires_at < @accessExpiry THEN expires_at ELSE @accessExpiry END,
              refresh_expires_at = CASE WHEN expires_at < @refreshExpiry THEN expires_at ELSE @refreshExpiry END
            WHERE home_id = @home AND refresh_hash = @old AND refresh_expires_at > @now AND expires_at > @now AND revoked_at IS NULL
            RETURNING id, access_expires_at, refresh_expires_at, expires_at;
            """, ("access", CompanionTokens.Hash(access)), ("refresh", CompanionTokens.Hash(refresh)),
            ("accessExpiry", (now + options.AccessLifetime).ToUnixTimeMilliseconds()), ("refreshExpiry", (now + options.RefreshLifetime).ToUnixTimeMilliseconds()),
            ("home", options.HomeId), ("old", hash), ("now", now.ToUnixTimeMilliseconds())))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                id = reader.GetString(0); accessExpiry = reader.GetInt64(1); refreshExpiry = reader.GetInt64(2); expiry = reader.GetInt64(3);
            }
        }
        if (id is not null)
        {
            await using var history = Command(connection, transaction,
                "INSERT INTO companion_refresh_history(hash, session_id, expires_at) VALUES (@hash, @id, @expiry);",
                ("hash", hash), ("id", id), ("expiry", expiry));
            await history.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // A spent refresh token signals possible theft. Revoke its entire device session, including the newly rotated access token.
            await using var revoke = Command(connection, transaction, """
                UPDATE companion_sessions SET revoked_at = @now WHERE home_id = @home AND revoked_at IS NULL AND id IN
                  (SELECT session_id FROM companion_refresh_history WHERE hash = @hash AND expires_at > @now);
                """, ("home", options.HomeId), ("hash", hash), ("now", now.ToUnixTimeMilliseconds()));
            await revoke.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return id is null ? null : new(id, access, DateTimeOffset.FromUnixTimeMilliseconds(accessExpiry), refresh, DateTimeOffset.FromUnixTimeMilliseconds(refreshExpiry));
    }

    public async Task<bool> RevokeAsync(string sessionId, string homeId, CancellationToken cancellationToken = default)
    {
        if (homeId != options.HomeId) { return false; }
        await using var connection = await openConnection(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, null,
            "UPDATE companion_sessions SET revoked_at = @now WHERE id = @id AND home_id = @home AND revoked_at IS NULL;",
            ("now", clock.GetUtcNow().ToUnixTimeMilliseconds()), ("id", sessionId), ("home", homeId));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    private CompanionSessionTokens NewTokens(string id, DateTimeOffset now, DateTimeOffset expiresAt) => new(id,
        CompanionTokens.Create(CompanionTokens.AccessPrefix), now + options.AccessLifetime < expiresAt ? now + options.AccessLifetime : expiresAt,
        CompanionTokens.Create(CompanionTokens.RefreshPrefix), now + options.RefreshLifetime < expiresAt ? now + options.RefreshLifetime : expiresAt);

    public async Task<int> PruneExpiredAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await openConnection(cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow().ToUnixTimeMilliseconds();
        await using var history = Command(connection, null, """
            DELETE FROM companion_refresh_history WHERE hash IN
              (SELECT hash FROM companion_refresh_history WHERE expires_at <= @now ORDER BY expires_at LIMIT 500);
            """, ("now", now));
        var removed = await history.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await using var sessions = Command(connection, null, """
            DELETE FROM companion_sessions WHERE id IN
              (SELECT id FROM companion_sessions WHERE expires_at <= @now ORDER BY expires_at LIMIT 100);
            """, ("now", now));
        return removed + await sessions.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static DbCommand Command(DbConnection connection, DbTransaction? transaction, string sql, params (string Name, object Value)[] values)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = connection is Npgsql.NpgsqlConnection ? CompanionSchema.QualifyPostgres(sql) : sql;
        foreach (var (name, value) in values)
        {
            var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value; command.Parameters.Add(parameter);
        }
        return command;
    }
}

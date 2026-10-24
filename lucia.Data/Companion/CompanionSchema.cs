using System.Text.RegularExpressions;

namespace lucia.Data.Companion;

/// <summary>Portable SQLite/PostgreSQL configuration schema. Times are UTC Unix milliseconds.</summary>
public static class CompanionSchema
{
    public static string QualifyPostgres(string sql) => Regex.Replace(sql, @"\b(companion_sessions|companion_refresh_history|companion_pairings|companion_pairing_gates)\b", "public.$1", RegexOptions.CultureInvariant);
    public const string Sql = """
        CREATE TABLE IF NOT EXISTS companion_sessions (
            id TEXT PRIMARY KEY,
            home_id TEXT NOT NULL,
            device_name TEXT NOT NULL,
            grant_json TEXT NOT NULL,
            access_hash TEXT NOT NULL UNIQUE,
            access_expires_at BIGINT NOT NULL,
            refresh_hash TEXT NOT NULL UNIQUE,
            refresh_expires_at BIGINT NOT NULL,
            expires_at BIGINT NOT NULL,
            revoked_at BIGINT
        );
        CREATE INDEX IF NOT EXISTS idx_companion_sessions_home ON companion_sessions(home_id);
        CREATE INDEX IF NOT EXISTS idx_companion_sessions_expiry ON companion_sessions(expires_at);
        CREATE TABLE IF NOT EXISTS companion_refresh_history (
            hash TEXT PRIMARY KEY,
            session_id TEXT NOT NULL,
            expires_at BIGINT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_companion_refresh_expiry ON companion_refresh_history(expires_at);
        """;
}

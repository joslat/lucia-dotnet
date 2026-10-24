namespace lucia.Data.Companion;

public static class CompanionPairingSchema
{
    public const string Sql = """
        CREATE TABLE IF NOT EXISTS companion_pairing_gates (home_id TEXT PRIMARY KEY, revision BIGINT NOT NULL);
        CREATE TABLE IF NOT EXISTS companion_pairings (
            id TEXT PRIMARY KEY,
            home_id TEXT NOT NULL,
            device_name TEXT NOT NULL,
            device_hash TEXT NOT NULL UNIQUE,
            user_hash TEXT NOT NULL UNIQUE,
            expires_at BIGINT NOT NULL,
            next_poll_at BIGINT NOT NULL,
            status TEXT NOT NULL CHECK (status IN ('Pending', 'Approved', 'Denied', 'Consumed')),
            grant_json TEXT,
            approver_id TEXT
        );
        CREATE INDEX IF NOT EXISTS idx_companion_pairings_expiry ON companion_pairings(expires_at);
        CREATE INDEX IF NOT EXISTS idx_companion_pairings_home ON companion_pairings(home_id, expires_at);
        """;
}

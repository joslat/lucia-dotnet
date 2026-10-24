namespace lucia.Data.Companion;

public static class CompanionLayoutSchema
{
    public const string Sql = """
        CREATE TABLE IF NOT EXISTS companion_layouts (
            home_id TEXT PRIMARY KEY,
            revision TEXT NOT NULL,
            document_json TEXT NOT NULL,
            updated_at BIGINT NOT NULL,
            updated_by TEXT NOT NULL
        );
        """;
}

using lucia.Data.Sqlite;
using lucia.Data.Companion;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace lucia.Tests.Data;

public sealed class SqliteCompanionMigrationTests
{
    [Fact]
    public void LegacyConfigImportPreservesSessionRevocationAndSpentRefreshMetadata()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-companion-import-");
        var path = Path.Combine(directory.FullName, "legacy.db");
        try
        {
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = CompanionSchema.Sql + """
                    INSERT INTO companion_sessions VALUES ('device', 'home-a', 'Synthetic phone', '{}', 'access-hash', 1, 'refresh-hash', 2, 3, 1);
                    INSERT INTO companion_refresh_history VALUES ('spent-hash', 'device', 3);
                    """;
                command.ExecuteNonQuery();
            }
            var imported = SqliteDbNames.GetConfigPath(path);
            using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = imported, Pooling = false }.ToString());
            target.Open();
            using var query = target.CreateCommand();
            query.CommandText = "SELECT revoked_at FROM companion_sessions WHERE id = 'device';";
            Assert.Equal(1L, query.ExecuteScalar());
            query.CommandText = "SELECT session_id FROM companion_refresh_history WHERE hash = 'spent-hash';";
            Assert.Equal("device", query.ExecuteScalar());
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task VersionThreeUpgradeAddsSessionTablesWithoutReplacingConfiguration()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-companion-migration-");
        var config = new SqliteConnectionFactory(Path.Combine(directory.FullName, "config.db"));
        var traces = new SqliteConnectionFactory(Path.Combine(directory.FullName, "traces.db"));
        var tasks = new SqliteConnectionFactory(Path.Combine(directory.FullName, "tasks.db"));
        try
        {
            var runner = new SqliteMigrationRunner(config, traces, tasks, NullLogger<SqliteMigrationRunner>.Instance);
            await runner.StartAsync(CancellationToken.None);
            using (var connection = config.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT INTO configuration(key, value) VALUES ('keep', 'unchanged');
                    DROP TABLE companion_sessions;
                    DROP TABLE companion_refresh_history;
                    UPDATE schema_version SET version = 3 WHERE id = 1;
                    """;
                command.ExecuteNonQuery();
            }
            await runner.StartAsync(CancellationToken.None);
            await runner.StartAsync(CancellationToken.None);
            using (var connection = config.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT version FROM schema_version;";
                Assert.Equal(4L, command.ExecuteScalar());
                command.CommandText = "SELECT value FROM configuration WHERE key = 'keep';";
                Assert.Equal("unchanged", command.ExecuteScalar());
                command.CommandText = "SELECT COUNT(*) FROM companion_sessions;";
                Assert.Equal(0L, command.ExecuteScalar());
                command.CommandText = "SELECT COUNT(*) FROM companion_refresh_history;";
                Assert.Equal(0L, command.ExecuteScalar());
            }
        }
        finally
        {
            foreach (var factory in new[] { config, traces, tasks })
            {
                factory.Dispose();
                using var connection = new SqliteConnection(factory.ConnectionString);
                SqliteConnection.ClearPool(connection);
            }
            directory.Delete(recursive: true);
        }
    }
}

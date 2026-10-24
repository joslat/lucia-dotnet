using lucia.Data.Sqlite;
using lucia.Data.Companion;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace lucia.Tests.Data;

public sealed class SqliteCompanionMigrationTests
{
    [Fact]
    public async Task VersionFiveUpgradeAddsSharedLayoutsAndRepeatedStartupPreservesThem()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-layout-v6-");
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
                command.CommandText = "DROP TABLE IF EXISTS companion_layouts; UPDATE schema_version SET version = 5 WHERE id = 1; INSERT INTO configuration(key,value) VALUES('keep-layout-upgrade','unchanged');";
                command.ExecuteNonQuery();
            }
            await runner.StartAsync(CancellationToken.None);
            var store = new RelationalCompanionLayoutStore(_ => ValueTask.FromResult<System.Data.Common.DbConnection>(config.CreateConnection()), TimeProvider.System);
            var saved = await store.TrySaveAsync(CompanionLayoutStoreTests.Layout("Keep shared layout"), null, "owner-a"); Assert.NotNull(saved);
            await runner.StartAsync(CancellationToken.None);
            Assert.Equal(saved.Revision, (await store.ReadAsync("home-a"))!.Revision);
            using var upgraded = config.CreateConnection(); using var query = upgraded.CreateCommand();
            query.CommandText = "SELECT version FROM schema_version;"; Assert.Equal(6L, query.ExecuteScalar());
            query.CommandText = "SELECT value FROM configuration WHERE key='keep-layout-upgrade';"; Assert.Equal("unchanged", query.ExecuteScalar());
        }
        finally
        {
            foreach (var factory in new[] { config, traces, tasks })
            { factory.Dispose(); using var connection = new SqliteConnection(factory.ConnectionString); SqliteConnection.ClearPool(connection); }
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void LegacyImportKeepsSharedLayoutBytesAndCheckedRevision()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-layout-import-");
        try
        {
            var path = Path.Combine(directory.FullName, "legacy.db");
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
            {
                connection.Open(); using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE companion_layouts(home_id TEXT PRIMARY KEY,revision TEXT NOT NULL,document_json TEXT NOT NULL,updated_at BIGINT NOT NULL,updated_by TEXT NOT NULL); INSERT INTO companion_layouts VALUES('home-a','checked-revision','preserve-exact-bytes',1,'owner-a');";
                command.ExecuteNonQuery();
            }
            var imported = SqliteDbNames.GetConfigPath(path);
            using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = imported, Pooling = false }.ToString()); target.Open();
            using var query = target.CreateCommand(); query.CommandText = "SELECT revision || ':' || document_json FROM companion_layouts WHERE home_id='home-a';";
            Assert.Equal("checked-revision:preserve-exact-bytes", query.ExecuteScalar());
        }
        finally { directory.Delete(recursive: true); }
    }

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
    public async Task VersionFourUpgradeAddsPairingWithoutReplacingSessions()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-companion-v5-");
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
                    INSERT INTO companion_sessions VALUES ('device', 'home-a', 'Synthetic phone', '{}', 'access-hash', 1, 'refresh-hash', 2, 3, 1);
                    INSERT INTO companion_refresh_history VALUES ('spent-hash', 'device', 3);
                    DROP TABLE companion_pairings;
                    DROP TABLE companion_pairing_gates;
                    UPDATE schema_version SET version = 4 WHERE id = 1;
                    """;
                command.ExecuteNonQuery();
            }
            await runner.StartAsync(CancellationToken.None);
            await runner.StartAsync(CancellationToken.None);
            using var upgraded = config.CreateConnection();
            using var query = upgraded.CreateCommand();
            query.CommandText = "SELECT version FROM schema_version;";
            Assert.Equal(6L, query.ExecuteScalar());
            query.CommandText = "SELECT revoked_at FROM companion_sessions WHERE id = 'device';";
            Assert.Equal(1L, query.ExecuteScalar());
            query.CommandText = "SELECT session_id FROM companion_refresh_history WHERE hash = 'spent-hash';";
            Assert.Equal("device", query.ExecuteScalar());
            query.CommandText = "SELECT COUNT(*) FROM companion_pairings;";
            Assert.Equal(0L, query.ExecuteScalar());
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
                Assert.Equal(6L, command.ExecuteScalar());
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

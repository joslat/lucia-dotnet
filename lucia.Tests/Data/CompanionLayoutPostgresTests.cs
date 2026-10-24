using System.Data.Common;
using lucia.Data.Companion;
using lucia.Data.PostgreSQL;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace lucia.Tests.Data;

public sealed class CompanionLayoutPostgresTests(PostgresMigrationFixture fixture) : IClassFixture<PostgresMigrationFixture>
{
    [Fact]
    public async Task CheckedWritesAndReopenUseTheMigratedPublicTableEvenWithAShadowedSearchPath()
    {
        await using var databases = await fixture.CreateDatabasesAsync(); await Runner(databases).StartAsync(CancellationToken.None);
        await using (var connection = await databases.Config.CreateConnectionAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE SCHEMA untrusted; CREATE TABLE untrusted.companion_layouts(home_id TEXT);";
            await command.ExecuteNonQueryAsync();
        }
        var connectionString = new NpgsqlConnectionStringBuilder(databases.Config.ConnectionString) { SearchPath = "untrusted,public", Pooling = false }.ToString();
        async ValueTask<DbConnection> Open(CancellationToken token)
        {
            var connection = new NpgsqlConnection(connectionString);
            try { await connection.OpenAsync(token); return connection; } catch { await connection.DisposeAsync(); throw; }
        }
        var first = new RelationalCompanionLayoutStore(Open, TimeProvider.System);
        var saved = await first.TrySaveAsync(CompanionLayoutStoreTests.Layout("First"), null, "owner-a"); Assert.NotNull(saved);
        var second = new RelationalCompanionLayoutStore(Open, TimeProvider.System);
        Assert.Equal(saved.Revision, (await second.ReadAsync("home-a"))!.Revision);
        var next = await second.TrySaveAsync(CompanionLayoutStoreTests.Layout("Second"), saved.Revision, "owner-b"); Assert.NotNull(next);
        Assert.Null(await first.TrySaveAsync(CompanionLayoutStoreTests.Layout("Stale"), saved.Revision, "owner-a"));
        Assert.Equal("Second", (await first.ReadAsync("home-a"))!.Layout.Name);
        Assert.Null(await first.ReadAsync("home-b"));
    }

    [Fact]
    public async Task ConcurrentCreatesAndReplacementsHaveOneWinnerAcrossIndependentConnections()
    {
        await using var databases = await fixture.CreateDatabasesAsync(); await Runner(databases).StartAsync(CancellationToken.None);
        RelationalCompanionLayoutStore Store() => new(async token => await databases.Config.CreateConnectionAsync(token), TimeProvider.System);
        var creates = await Task.WhenAll(Enumerable.Range(0, 5).Select(index => Store().TrySaveAsync(CompanionLayoutStoreTests.Layout($"Create {index}"), null, "owner-a")));
        var baseline = Assert.Single(creates, result => result is not null)!;
        var updates = await Task.WhenAll(Enumerable.Range(0, 5).Select(index => Store().TrySaveAsync(CompanionLayoutStoreTests.Layout($"Update {index}"), baseline.Revision, "owner-a")));
        var winner = Assert.Single(updates, result => result is not null)!;
        Assert.Equal(winner.Revision, (await Store().ReadAsync("home-a"))!.Revision);
    }

    [Fact]
    public async Task VersionFiveUpgradeAndRepeatedStartupPreserveExistingConfigAndTheNewLayout()
    {
        await using var databases = await fixture.CreateDatabasesAsync(); var runner = Runner(databases); await runner.StartAsync(CancellationToken.None);
        await using (var connection = await databases.Config.CreateConnectionAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "DROP TABLE IF EXISTS public.companion_layouts; UPDATE public.schema_version SET version=5 WHERE id=1; INSERT INTO public.configuration(key,value) VALUES('keep-layout-upgrade','unchanged');";
            await command.ExecuteNonQueryAsync();
        }
        await runner.StartAsync(CancellationToken.None);
        var store = new RelationalCompanionLayoutStore(async token => await databases.Config.CreateConnectionAsync(token), TimeProvider.System);
        var saved = await store.TrySaveAsync(CompanionLayoutStoreTests.Layout("Preserved"), null, "owner-a"); Assert.NotNull(saved);
        await runner.StartAsync(CancellationToken.None);
        Assert.Equal(saved.Revision, (await store.ReadAsync("home-a"))!.Revision);
        await using var verify = await databases.Config.CreateConnectionAsync(); await using var query = verify.CreateCommand();
        query.CommandText = "SELECT version FROM public.schema_version;"; Assert.Equal(6, await query.ExecuteScalarAsync());
        query.CommandText = "SELECT value FROM public.configuration WHERE key='keep-layout-upgrade';"; Assert.Equal("unchanged", await query.ExecuteScalarAsync());
    }

    private static PostgresMigrationRunner Runner(PostgresTestDatabases databases) => new(databases.Config, databases.Traces, databases.Tasks, NullLogger<PostgresMigrationRunner>.Instance);
}

using System.Data.Common;
using lucia.Agents.Companion;
using lucia.Data.Companion;
using lucia.Data.PostgreSQL;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace lucia.Tests.Data;

public sealed class PostgresCompanionSessionTests(PostgresMigrationFixture fixture) : IClassFixture<PostgresMigrationFixture>
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MigratedPostgresRotatesRevokesAndRejectsConcurrentReplay()
    {
        await using var databases = await fixture.CreateDatabasesAsync();
        await MigrateAsync(databases);
        var service = Service(databases.Config);
        var issued = await service.CreateAsync(new("home-a", "member-a", [new("living", [CompanionField.Contact])]), "Synthetic tablet");
        Assert.NotNull(await Service(databases.Config).AuthenticateAsync(issued.AccessToken));
        var results = await Task.WhenAll(Service(databases.Config).RenewAsync(issued.RefreshToken), Service(databases.Config).RenewAsync(issued.RefreshToken));
        var winner = Assert.Single(results, result => result is not null)!;
        Assert.Null(await service.AuthenticateAsync(winner.AccessToken));
        Assert.Null(await service.RenewAsync(winner.RefreshToken));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchPathCannotRedirectSessionStorage()
    {
        await using var databases = await fixture.CreateDatabasesAsync();
        await MigrateAsync(databases);
        await using (var connection = await databases.Config.CreateConnectionAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE SCHEMA evil; SET search_path TO evil; " + CompanionSchema.Sql;
            await command.ExecuteNonQueryAsync();
        }
        await using var shadowed = new PostgresConnectionFactory(new NpgsqlConnectionStringBuilder(databases.Config.ConnectionString) { SearchPath = "evil,public" }.ConnectionString);
        var issued = await Service(shadowed).CreateAsync(new("home-a", "member-a", []), "Synthetic phone");
        Assert.NotNull(await Service(databases.Config).AuthenticateAsync(issued.AccessToken));
    }

    private static RelationalCompanionSessionService Service(PostgresConnectionFactory factory) => new(
        async cancellationToken => (DbConnection)await factory.CreateConnectionAsync(cancellationToken),
        new() { Enabled = true, HomeId = "home-a" }, new FakeTimeProvider(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero)));

    private static Task MigrateAsync(PostgresTestDatabases databases) => new PostgresMigrationRunner(
        databases.Config, databases.Traces, databases.Tasks, NullLogger<PostgresMigrationRunner>.Instance).StartAsync(CancellationToken.None);
}

using System.Data.Common;
using lucia.Agents.Companion;
using lucia.Data.Companion;
using lucia.Data.PostgreSQL;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace lucia.Tests.Data;

public sealed class PostgresCompanionPairingTests(PostgresMigrationFixture fixture) : IClassFixture<PostgresMigrationFixture>
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task SeparateWritersRespectCapacityAndOnlyOneCanRedeem()
    {
        await using var databases = await fixture.CreateDatabasesAsync();
        await MigrateAsync(databases);
        var options = new CompanionOptions { Enabled = true, HomeId = "home-a", MaximumPendingPairings = 1 };
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));
        var starts = await Task.WhenAll(Pairings(databases.Config, options, clock).StartAsync("Phone"), Pairings(databases.Config, options, clock).StartAsync("Tablet"));
        var challenge = Assert.Single(starts, result => result is not null)!;
        var service = Pairings(databases.Config, options, clock);
        var review = (await service.ReviewAsync(challenge.UserCode))!;
        Assert.True(await service.ApproveAsync(review.Id, new("home-a", "member-a", [new("living", [CompanionField.Temperature])]), "owner-a"));
        var results = await Task.WhenAll(service.RedeemAsync(challenge.DeviceCode), Pairings(databases.Config, options, clock).RedeemAsync(challenge.DeviceCode));
        var success = Assert.Single(results, result => result.Status == CompanionPairingStatus.Success);
        Assert.Single(results, result => result.Status == CompanionPairingStatus.Consumed);
        var sessions = new RelationalCompanionSessionService(async cancellationToken => (DbConnection)await databases.Config.CreateConnectionAsync(cancellationToken), options, clock);
        Assert.NotNull(await sessions.AuthenticateAsync(success.Tokens!.AccessToken));
        Assert.Single(await sessions.ListActiveAsync("home-a"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ShadowedSchemaAndFailedIssuanceCannotLoseOrRedirectApproval()
    {
        await using var databases = await fixture.CreateDatabasesAsync();
        await MigrateAsync(databases);
        await ExecuteAsync(databases.Config, "CREATE SCHEMA evil; SET search_path TO evil; " + CompanionPairingSchema.Sql);
        await using var shadowed = new PostgresConnectionFactory(new NpgsqlConnectionStringBuilder(databases.Config.ConnectionString) { SearchPath = "evil,public" }.ConnectionString);
        var options = new CompanionOptions { Enabled = true, HomeId = "home-a" };
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));
        var challenge = (await Pairings(shadowed, options, clock).StartAsync("Synthetic failure"))!;
        var normal = Pairings(databases.Config, options, clock);
        var review = Assert.IsType<CompanionPairingReview>(await normal.ReviewAsync(challenge.UserCode));
        Assert.True(await normal.ApproveAsync(review.Id, new("home-a", "member-a", []), "owner-a"));
        await ExecuteAsync(databases.Config, "ALTER TABLE public.companion_sessions ADD CONSTRAINT fail_synthetic CHECK (device_name <> 'Synthetic failure');");
        await Assert.ThrowsAsync<PostgresException>(() => normal.RedeemAsync(challenge.DeviceCode));
        Assert.Equal("Approved", (await normal.ReviewAsync(challenge.UserCode))!.Status);
        await ExecuteAsync(databases.Config, "ALTER TABLE public.companion_sessions DROP CONSTRAINT fail_synthetic;");
        Assert.Equal(CompanionPairingStatus.Success, (await normal.RedeemAsync(challenge.DeviceCode)).Status);
    }

    private static RelationalCompanionPairingService Pairings(PostgresConnectionFactory factory, CompanionOptions options, TimeProvider clock) =>
        new(async cancellationToken => (DbConnection)await factory.CreateConnectionAsync(cancellationToken), options, clock);
    private static Task MigrateAsync(PostgresTestDatabases databases) => new PostgresMigrationRunner(
        databases.Config, databases.Traces, databases.Tasks, NullLogger<PostgresMigrationRunner>.Instance).StartAsync(CancellationToken.None);
    private static async Task ExecuteAsync(PostgresConnectionFactory factory, string sql)
    {
        await using var connection = await factory.CreateConnectionAsync();
        await using var command = connection.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync();
    }
}

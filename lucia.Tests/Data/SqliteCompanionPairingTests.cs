using System.Data.Common;
using lucia.Agents.Companion;
using lucia.Data.Companion;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace lucia.Tests.Data;

public sealed class SqliteCompanionPairingTests : IDisposable
{
    private readonly SqliteTestHelper database = new();
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));
    private readonly CompanionOptions options = new() { Enabled = true, HomeId = "home-a" };

    public SqliteCompanionPairingTests() => Execute(CompanionSchema.Sql + CompanionPairingSchema.Sql);

    [Fact]
    public async Task OnlyThePendingSecretCanRedeemTheExactOwnerApprovedGrantOnce()
    {
        var pairing = Service();
        var challenge = Assert.IsType<CompanionPairingChallenge>(await pairing.StartAsync("Synthetic phone"));
        Assert.DoesNotContain(challenge.DeviceCode, challenge.ToString());
        Assert.Null(await pairing.ReviewAsync("00000-00000"));
        Assert.Equal(CompanionPairingStatus.Invalid, (await pairing.RedeemAsync(challenge.UserCode)).Status);
        var review = Assert.IsType<CompanionPairingReview>(await pairing.ReviewAsync(challenge.UserCode.ToLowerInvariant()));
        Assert.Equal("Synthetic phone", review.DeviceName);
        Assert.Equal(CompanionPairingStatus.Pending, (await pairing.RedeemAsync(challenge.DeviceCode)).Status);
        Assert.True(await pairing.ApproveAsync(review.Id, Grant(), "owner-a"));
        Assert.False(await pairing.ApproveAsync(review.Id, new("home-a", "other-member", [], canReadEngine: true), "owner-a"));
        clock.Advance(TimeSpan.FromSeconds(5));
        var result = await Service().RedeemAsync(challenge.DeviceCode);
        Assert.Equal(CompanionPairingStatus.Success, result.Status);
        var tokens = Assert.IsType<CompanionSessionTokens>(result.Tokens);
        var session = await Sessions().AuthenticateAsync(tokens.AccessToken);
        Assert.NotNull(session);
        Assert.Equal("member-a", session.Grant.MemberId);
        Assert.True(session.Grant.Allows("home-a", "living", CompanionField.Temperature));
        Assert.False(session.Grant.CanReadEngine);
        Assert.Equal(CompanionPairingStatus.Consumed, (await pairing.RedeemAsync(challenge.DeviceCode)).Status);
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM companion_sessions;"));
        Assert.NotEqual(challenge.DeviceCode, Scalar("SELECT device_hash FROM companion_pairings;"));
        Assert.NotEqual(challenge.UserCode.Replace("-", ""), Scalar("SELECT user_hash FROM companion_pairings;"));
    }

    [Fact]
    public async Task ApprovalExpiresCanBeCancelledAndDurablePollingIsThrottled()
    {
        var challenge = (await Service().StartAsync("Phone"))!;
        Assert.Equal(CompanionPairingStatus.Pending, (await Service().RedeemAsync(challenge.DeviceCode)).Status);
        Assert.Equal(CompanionPairingStatus.SlowDown, (await Service().RedeemAsync(challenge.DeviceCode)).Status);
        var review = (await Service().ReviewAsync(challenge.UserCode))!;
        await Assert.ThrowsAsync<ArgumentException>(() => Service().ApproveAsync(review.Id, new("home-b", "member-a", []), "owner-a"));
        Assert.True(await Service().ApproveAsync(review.Id, Grant(), "owner-a"));
        Assert.True(await Service().CancelAsync(review.Id));
        Assert.Equal(CompanionPairingStatus.Denied, (await Service().RedeemAsync(challenge.DeviceCode)).Status);
        var expired = (await Service().StartAsync("Tablet"))!;
        var expiredReview = (await Service().ReviewAsync(expired.UserCode))!;
        clock.Advance(options.PairingLifetime);
        Assert.False(await Service().ApproveAsync(expiredReview.Id, Grant(), "owner-a"));
        Assert.Equal(CompanionPairingStatus.Expired, (await Service().RedeemAsync(expired.DeviceCode)).Status);
        Assert.Null(await Service().ReviewAsync(expired.UserCode));
    }

    [Fact]
    public async Task ConcurrentRedemptionCreatesExactlyOneSession()
    {
        var challenge = (await Service().StartAsync("Phone"))!;
        var review = (await Service().ReviewAsync(challenge.UserCode))!;
        Assert.True(await Service().ApproveAsync(review.Id, Grant(), "owner-a"));
        using var start = new Barrier(2);
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(10)));
            return await Service().RedeemAsync(challenge.DeviceCode);
        })));
        Assert.Single(results, result => result.Status == CompanionPairingStatus.Success);
        Assert.Single(results, result => result.Status == CompanionPairingStatus.Consumed);
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM companion_sessions;"));
    }

    [Fact]
    public async Task FailedIssuanceRollsBackConsumptionAndCanRetry()
    {
        var challenge = (await Service().StartAsync("Phone"))!;
        var review = (await Service().ReviewAsync(challenge.UserCode))!;
        Assert.True(await Service().ApproveAsync(review.Id, Grant(), "owner-a"));
        Execute("CREATE TRIGGER fail_session BEFORE INSERT ON companion_sessions BEGIN SELECT RAISE(ABORT, 'synthetic write failure'); END;");
        await Assert.ThrowsAsync<SqliteException>(() => Service().RedeemAsync(challenge.DeviceCode));
        Assert.Equal("Approved", Scalar("SELECT status FROM companion_pairings;"));
        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM companion_sessions;"));
        Execute("DROP TRIGGER fail_session;");
        Assert.Equal(CompanionPairingStatus.Success, (await Service().RedeemAsync(challenge.DeviceCode)).Status);
    }

    [Fact]
    public async Task PendingCapacityIsDurableAndExpiredIntentsReleaseIt()
    {
        options.MaximumPendingPairings = 1;
        Assert.NotNull(await Service().StartAsync("Phone"));
        Assert.Null(await Service().StartAsync("Other phone"));
        clock.Advance(options.PairingLifetime);
        Assert.NotNull(await Service().StartAsync("Other phone"));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM companion_pairings;"));
        Assert.Equal(0, await Service().PruneExpiredAsync());
    }

    private RelationalCompanionPairingService Service() => new(Open, options, clock);
    private RelationalCompanionSessionService Sessions() => new(Open, options, clock);
    private ValueTask<DbConnection> Open(CancellationToken _) => ValueTask.FromResult<DbConnection>(database.ConnectionFactory.CreateConnection());
    private static CompanionGrant Grant() => new("home-a", "member-a", [new("living", [CompanionField.Temperature])]);
    private void Execute(string sql)
    {
        using var connection = database.ConnectionFactory.CreateConnection();
        using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
    }
    private object? Scalar(string sql)
    {
        using var connection = database.ConnectionFactory.CreateConnection();
        using var command = connection.CreateCommand(); command.CommandText = sql; return command.ExecuteScalar();
    }
    public void Dispose() => database.Dispose();
}

using System.Data.Common;
using lucia.Agents.Companion;
using lucia.Data.Companion;
using Microsoft.Extensions.Time.Testing;

namespace lucia.Tests.Data;

public sealed class SqliteCompanionSessionTests : IDisposable
{
    private readonly SqliteTestHelper helper = new();
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));
    private readonly CompanionOptions options = new() { Enabled = true, HomeId = "home-a" };

    public SqliteCompanionSessionTests()
    {
        using var connection = helper.ConnectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = CompanionSchema.Sql;
        command.ExecuteNonQuery();
    }

    [Fact]
    public async Task CredentialsAreDurableHashedAndCannotActAsEachOther()
    {
        var service = CreateService();
        var issued = await service.CreateAsync(Grant(), "Synthetic phone");
        Assert.DoesNotContain(issued.AccessToken, issued.ToString());
        Assert.DoesNotContain(issued.RefreshToken, issued.ToString());
        var session = await CreateService().AuthenticateAsync(issued.AccessToken);
        Assert.NotNull(session);
        Assert.Equal("member-a", session.Grant.MemberId);
        Assert.True(session.Grant.Allows("home-a", "living", CompanionField.Temperature));
        Assert.Null(await service.AuthenticateAsync(issued.RefreshToken));
        Assert.Null(await service.RenewAsync(issued.AccessToken));
        using var connection = helper.ConnectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT access_hash, refresh_hash, grant_json FROM companion_sessions;";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.NotEqual(issued.AccessToken, reader.GetString(0));
        Assert.NotEqual(issued.RefreshToken, reader.GetString(1));
        Assert.DoesNotContain(issued.AccessToken, reader.GetString(2));
        Assert.DoesNotContain(issued.RefreshToken, reader.GetString(2));
    }

    [Fact]
    public async Task AccessExpiresAndRefreshRotatesWithoutExtendingAbsoluteSessionLifetime()
    {
        var service = CreateService();
        var issued = await service.CreateAsync(Grant(), "Synthetic phone");
        clock.Advance(options.AccessLifetime);
        Assert.Null(await service.AuthenticateAsync(issued.AccessToken));
        var renewed = await service.RenewAsync(issued.RefreshToken);
        Assert.NotNull(renewed);
        Assert.NotEqual(issued.AccessToken, renewed.AccessToken);
        Assert.NotEqual(issued.RefreshToken, renewed.RefreshToken);
        Assert.NotNull(await service.AuthenticateAsync(renewed.AccessToken));
        clock.Advance(options.SessionLifetime);
        Assert.Null(await service.RenewAsync(renewed.RefreshToken));
        Assert.Null(await service.AuthenticateAsync(renewed.AccessToken));
    }

    [Fact]
    public async Task ReplayedRefreshRevokesTheWholeDeviceSession()
    {
        var service = CreateService();
        var issued = await service.CreateAsync(Grant(), "Synthetic phone");
        var renewed = await service.RenewAsync(issued.RefreshToken);
        Assert.NotNull(renewed);
        Assert.Null(await service.RenewAsync(issued.RefreshToken));
        Assert.Null(await CreateService().AuthenticateAsync(renewed.AccessToken));
        Assert.Null(await service.RenewAsync(renewed.RefreshToken));
    }

    [Fact]
    public async Task RevocationIsImmediateAcrossInstancesAndHomeScoped()
    {
        var service = CreateService();
        var issued = await service.CreateAsync(Grant(), "Synthetic phone");
        Assert.False(await CreateService().RevokeAsync(issued.SessionId, "home-b"));
        Assert.NotNull(await service.AuthenticateAsync(issued.AccessToken));
        Assert.True(await CreateService().RevokeAsync(issued.SessionId, "home-a"));
        Assert.Null(await service.AuthenticateAsync(issued.AccessToken));
        Assert.Null(await service.RenewAsync(issued.RefreshToken));
    }

    [Fact]
    public async Task DisabledOrDifferentHomeCannotIssueAndIdleRefreshExpires()
    {
        options.Enabled = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateAsync(Grant(), "Phone"));
        options.Enabled = true;
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService().CreateAsync(new("home-b", "member-a", []), "Phone"));
        var issued = await CreateService().CreateAsync(Grant(), "Phone");
        clock.Advance(options.RefreshLifetime);
        Assert.Null(await CreateService().RenewAsync(issued.RefreshToken));
    }

    [Fact]
    public async Task SimultaneousRefreshHasOneWinnerAndRevokesItOnReplay()
    {
        var issued = await CreateService().CreateAsync(Grant(), "Synthetic phone");
        using var start = new Barrier(2);
        var attempts = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(10)));
            return await CreateService().RenewAsync(issued.RefreshToken);
        }));
        var results = await Task.WhenAll(attempts);
        var winner = Assert.Single(results, result => result is not null)!;
        Assert.Null(await CreateService().AuthenticateAsync(winner.AccessToken));
        Assert.Null(await CreateService().RenewAsync(winner.RefreshToken));
    }

    [Fact]
    public async Task ExpiredMetadataAndReplayHistoryCanBePrunedWithoutRemovingAnActiveSession()
    {
        var service = CreateService();
        var old = await service.CreateAsync(Grant(), "Old synthetic phone");
        Assert.NotNull(await service.RenewAsync(old.RefreshToken));
        clock.Advance(options.SessionLifetime);
        var current = await service.CreateAsync(Grant(), "Current synthetic phone");
        Assert.Equal(2, await service.PruneExpiredAsync());
        Assert.NotNull(await service.AuthenticateAsync(current.AccessToken));
        Assert.Equal(0, await service.PruneExpiredAsync());
    }

    private RelationalCompanionSessionService CreateService() => new(
        _ => ValueTask.FromResult<DbConnection>(helper.ConnectionFactory.CreateConnection()), options, clock);

    private static CompanionGrant Grant() => new("home-a", "member-a", [new("living", [CompanionField.Temperature])]);
    public void Dispose() => helper.Dispose();
}

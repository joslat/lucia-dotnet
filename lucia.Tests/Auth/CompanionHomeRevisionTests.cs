using System.Net.Http.Headers;
using FakeItEasy;
using lucia.Agents.Companion;
using lucia.HomeAssistant.Models;
using Microsoft.Extensions.Time.Testing;

namespace lucia.Tests.Auth;

public sealed class CompanionHomeRevisionTests
{
    [Fact]
    public async Task IndependentReadersReceiveTheSameScopedRevisionWithoutConsumingEachOthersUpdates()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        CompanionHomeApiTests.ConfigureHome(host);
        var tokens = await host.Sessions.CreateAsync(Grant(), "Phone");
        using var first = await ReadAsync(host, tokens.AccessToken);
        var revision = Assert.IsType<EntityTagHeaderValue>(first.Headers.ETag);
        Assert.True(revision.IsWeak);
        Assert.Matches("^\"[A-F0-9]{64}\"$", revision.Tag);
        using var second = await ReadAsync(host, tokens.AccessToken);
        Assert.Equal(revision, second.Headers.ETag);
        using var unchanged = await ReadAsync(host, tokens.AccessToken, revision.Tag);
        Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);
        Assert.Equal(revision, unchanged.Headers.ETag);
        Assert.True(unchanged.Headers.CacheControl?.NoStore);
        Assert.Empty(await unchanged.Content.ReadAsByteArrayAsync());
        // Both readers revalidate against the source. No shared single-consumer event queue or cached authority.
        A.CallTo(() => host.HomeAssistant.GetStatesAsync(A<CancellationToken>._)).MustHaveHappened(3, Times.Exactly);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlyAuthorizedChangesAlterTheRevisionAndAnyMissedRevisionGetsACompleteSnapshot(bool visibleChange)
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        CompanionHomeApiTests.ConfigureHome(host);
        var states = new[] { State("sensor.porch_temperature", "21.5"), State("sensor.bedroom_temperature", "19") };
        A.CallTo(() => host.HomeAssistant.GetStatesAsync(A<CancellationToken>._)).ReturnsLazily(() => states);
        var tokens = await host.Sessions.CreateAsync(Grant(), "Phone");
        using var first = await ReadAsync(host, tokens.AccessToken);
        var revision = Assert.IsType<EntityTagHeaderValue>(first.Headers.ETag).Tag;
        states = [State("sensor.porch_temperature", visibleChange ? "22" : "21.5"), State("sensor.bedroom_temperature", "25")];
        using var next = await ReadAsync(host, tokens.AccessToken, revision);
        Assert.Equal(visibleChange ? HttpStatusCode.OK : HttpStatusCode.NotModified, next.StatusCode);
        if (visibleChange)
        {
            Assert.NotEqual(revision, next.Headers.ETag!.Tag);
            Assert.Contains("22", await next.Content.ReadAsStringAsync());
        }
        // A restart, unknown cursor or any number of missed changes always resynchronizes the whole allowed view.
        using var resync = await ReadAsync(host, tokens.AccessToken, "\"" + new string('0', 64) + "\"");
        Assert.Equal(HttpStatusCode.OK, resync.StatusCode);
        Assert.Contains("porch", await resync.Content.ReadAsStringAsync());
        Assert.DoesNotContain("bedroom", await resync.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ARevisionIsBoundToTheDeviceSessionEvenWhenItsVisibleDataAndGrantsMatch()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        CompanionHomeApiTests.ConfigureHome(host);
        var phone = await host.Sessions.CreateAsync(Grant(), "Phone");
        var tablet = await host.Sessions.CreateAsync(Grant(), "Tablet");
        using var first = await ReadAsync(host, phone.AccessToken);
        var revision = Assert.IsType<EntityTagHeaderValue>(first.Headers.ETag).Tag;
        using var other = await ReadAsync(host, tablet.AccessToken, revision);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        Assert.NotEqual(revision, other.Headers.ETag!.Tag);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AConditionalReadNeverHidesSourceFailureOrRevocationDuringTheRead(bool revoked)
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        CompanionHomeApiTests.ConfigureHome(host);
        var tokens = await host.Sessions.CreateAsync(Grant(), "Phone");
        using var first = await ReadAsync(host, tokens.AccessToken);
        var revision = Assert.IsType<EntityTagHeaderValue>(first.Headers.ETag).Tag;
        A.CallTo(() => host.HomeAssistant.GetStatesAsync(A<CancellationToken>._)).ReturnsLazily(async _ =>
        {
            if (revoked) { await host.Sessions.RevokeAsync(tokens.SessionId, "home-a"); }
            throw new HttpRequestException("private-source-failure");
        });
        using var next = await ReadAsync(host, tokens.AccessToken, revision);
        Assert.Equal(revoked ? HttpStatusCode.Unauthorized : HttpStatusCode.ServiceUnavailable, next.StatusCode);
        Assert.Null(next.Headers.ETag);
        Assert.DoesNotContain("private-source-failure", await next.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AgingAReportChangesTheRevisionWithoutInventingANewReportTimestamp()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));
        await using var host = await CompanionSecurityTestHost.StartAsync(clock);
        CompanionHomeApiTests.ConfigureHome(host);
        var state = State("sensor.porch_temperature", "21.5");
        state.LastUpdated = clock.GetUtcNow().UtcDateTime;
        A.CallTo(() => host.HomeAssistant.GetStatesAsync(A<CancellationToken>._)).Returns([state]);
        var tokens = await host.Sessions.CreateAsync(Grant(), "Phone");
        using var first = await ReadAsync(host, tokens.AccessToken);
        var revision = Assert.IsType<EntityTagHeaderValue>(first.Headers.ETag).Tag;
        clock.Advance(TimeSpan.FromMinutes(31));
        // Refresh credentials without changing the device session to isolate freshness from access expiry.
        var renewed = await host.Sessions.RenewAsync(tokens.RefreshToken);
        using var aged = await ReadAsync(host, renewed!.AccessToken, revision);
        Assert.Equal(HttpStatusCode.OK, aged.StatusCode);
        Assert.NotEqual(revision, aged.Headers.ETag!.Tag);
        Assert.Contains("Stale", await aged.Content.ReadAsStringAsync());
    }

    private static CompanionGrant Grant() => new("home-a", "member-a", [new("porch", [CompanionField.Temperature])]);
    private static HomeAssistantState State(string id, string value)
    {
        var state = CompanionHomeApiTests.State(id, value, "temperature", "°C");
        state.LastUpdated = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
        return state;
    }
    private static Task<HttpResponseMessage> ReadAsync(CompanionSecurityTestHost host, string access, string? revision = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/companion/homes/home-a/snapshot");
        request.Headers.Authorization = new("Bearer", access);
        if (revision is not null) { request.Headers.IfNoneMatch.Add(new(revision, isWeak: true)); }
        return SendAsync();
        async Task<HttpResponseMessage> SendAsync()
        {
            using (request) { return await host.Client.SendAsync(request); }
        }
    }
}

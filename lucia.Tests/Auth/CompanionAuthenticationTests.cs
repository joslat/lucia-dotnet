using System.Net.Http.Headers;
using lucia.Agents.Companion;

namespace lucia.Tests.Auth;

public sealed class CompanionAuthenticationTests
{
    [Fact]
    public async Task NativeCredentialCannotReachGeneralOwnerOrInternalEndpoints()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        var tokens = await host.Sessions.CreateAsync(new("home-a", "member-a", [], canReadEngine: true), "Synthetic phone");
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        foreach (var path in new[] { "/general", "/owner", "/internal" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync(path)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/companion/homes/home-a/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/companion/homes/home-b/session")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/engine/home-a")).StatusCode);
    }

    [Theory]
    [InlineData("owner-header")]
    [InlineData("owner-cookie")]
    [InlineData("internal")]
    public async Task BroadCredentialsAreNotACompanionIdentity(string kind)
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        if (kind == "owner-header") { host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner"); }
        if (kind == "owner-cookie") { host.Client.DefaultRequestHeaders.Add("Cookie", ".lucia.session=synthetic-cookie"); }
        if (kind == "internal") { host.Client.DefaultRequestHeaders.Authorization = new("Bearer", "synthetic-internal"); }
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/companion/homes/home-a/session")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync(kind == "internal" ? "/internal" : "/owner")).StatusCode);
    }

    [Fact]
    public async Task MemberCannotGainEngineHistoryActivityAndRevocationIsImmediate()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        var tokens = await host.Sessions.CreateAsync(new("home-a", "member-a", [new("living", [CompanionField.Temperature])]), "Synthetic phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        foreach (var path in new[] { "/engine/home-a", "/history/home-a", "/activity/home-a" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync(path)).StatusCode);
        }
        host.Client.DefaultRequestHeaders.Add("Cookie", ".lucia.session=synthetic-cookie");
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/engine/home-a")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/owner")).StatusCode);
        var json = await host.Client.GetStringAsync("/api/companion/homes/home-a/session");
        Assert.Contains("member-a", json);
        Assert.DoesNotContain(tokens.AccessToken, json);
        Assert.DoesNotContain(tokens.RefreshToken, json);
        var response = await host.Client.GetAsync("/api/companion/homes/home-a/session");
        Assert.True(response.Headers.CacheControl?.NoStore);
        await host.Sessions.RevokeAsync(tokens.SessionId, "home-a");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/companion/homes/home-a/session")).StatusCode);
    }
}

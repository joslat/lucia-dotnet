using lucia.Agents.Companion;
using System.Net.Http.Json;

namespace lucia.Tests.Auth;

public sealed class CompanionRefreshRevocationTests
{
    [Fact]
    public async Task CurrentOrSpentRefreshProofCanRevokeOnlyItsOwnSessionWithoutIssuingNewCredentials()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        var phone = await host.Sessions.CreateAsync(new("home-a", "member-a", []), "Phone");
        var tablet = await host.Sessions.CreateAsync(new("home-a", "member-b", []), "Tablet");
        var rotated = (await host.Sessions.RenewAsync(phone.RefreshToken))!;
        var response = await host.Client.PostAsJsonAsync("/api/companion/session/revoke", new { refreshToken = phone.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Null(await host.Sessions.AuthenticateAsync(rotated.AccessToken));
        Assert.Null(await host.Sessions.RenewAsync(rotated.RefreshToken));
        Assert.NotNull(await host.Sessions.AuthenticateAsync(tablet.AccessToken));
        // Idempotent and deliberately no existence oracle.
        Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PostAsJsonAsync("/api/companion/session/revoke", new { refreshToken = "lcr_" + new string('z', 43) })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PostAsJsonAsync("/api/companion/session/revoke", new { refreshToken = phone.RefreshToken })).StatusCode);
    }
}

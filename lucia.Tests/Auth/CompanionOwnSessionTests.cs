using System.Net.Http.Json;
using System.Text.Json;
using lucia.Agents.Companion;

namespace lucia.Tests.Auth;

public sealed class CompanionOwnSessionTests
{
    [Fact]
    public async Task DeviceCanLearnItsApprovedHomeAndLogoutWithoutAnUntrustedHomeHint()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        var issued = await host.Sessions.CreateAsync(new("home-a", "member-a", [new("living", [CompanionField.Contact])]), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", issued.AccessToken);
        var response = await host.Client.GetAsync("/api/companion/session");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var own = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("home-a", own.GetProperty("homeId").GetString());
        Assert.DoesNotContain(issued.AccessToken, await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PostAsync("/api/companion/session/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/companion/session")).StatusCode);
        host.Client.DefaultRequestHeaders.Authorization = null;
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/companion/session")).StatusCode);
    }
}

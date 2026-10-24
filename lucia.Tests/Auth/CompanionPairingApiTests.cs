using System.Net.Http.Json;
using System.Text.Json;
using lucia.Agents.Companion;
using lucia.AgentHost.Companion;
using FakeItEasy;

namespace lucia.Tests.Auth;

public sealed class CompanionPairingApiTests
{
    [Fact]
    public void CredentialRequestsDoNotPrintSecrets()
    {
        Assert.DoesNotContain("sensitive-device-code", new CompanionPairingRedeemRequest("sensitive-device-code").ToString());
        Assert.DoesNotContain("sensitive-refresh-code", new CompanionRenewRequest("sensitive-refresh-code").ToString());
        Assert.DoesNotContain("SENSI-TIVE0", new CompanionPairingReviewRequest("SENSI-TIVE0").ToString());
    }
    [Fact]
    public async Task OwnerApprovesButOnlyDeviceReceivesCredentialsAndLogoutRevokesThem()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        var start = await host.Client.PostAsJsonAsync("/api/companion/pairings", new { deviceName = "Synthetic phone" });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        Assert.True(start.Headers.CacheControl?.NoStore);
        var challenge = await start.Content.ReadFromJsonAsync<JsonElement>();
        var deviceCode = challenge.GetProperty("deviceCode").GetString()!;
        var userCode = challenge.GetProperty("userCode").GetString()!;
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        var review = await host.Client.PostAsJsonAsync("/api/companion/owner/pairings/review", new { userCode });
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        Assert.DoesNotContain(deviceCode, await review.Content.ReadAsStringAsync());
        var id = (await review.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        var approval = await host.Client.PostAsJsonAsync($"/api/companion/owner/pairings/{id}/approve", new
        {
            codeConfirmed = true,
            grant = new { homeId = "home-a", memberId = "member-a", places = new[] { new { placeId = "living", fields = new[] { "Temperature" } } } }
        });
        Assert.Equal(HttpStatusCode.NoContent, approval.StatusCode);
        Assert.Empty(await approval.Content.ReadAsStringAsync());
        host.Client.DefaultRequestHeaders.Remove("X-API-Key");
        var redeem = await host.Client.PostAsJsonAsync("/api/companion/pairings/redeem", new { deviceCode });
        Assert.Equal(HttpStatusCode.OK, redeem.StatusCode);
        var tokens = (await redeem.Content.ReadFromJsonAsync<JsonElement>());
        var access = tokens.GetProperty("accessToken").GetString()!;
        var refresh = tokens.GetProperty("refreshToken").GetString()!;
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", access);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/companion/homes/home-a/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/engine/home-a")).StatusCode);
        var renewedResponse = await host.Client.PostAsJsonAsync("/api/companion/session/renew", new { refreshToken = refresh });
        Assert.Equal(HttpStatusCode.OK, renewedResponse.StatusCode);
        var renewed = await renewedResponse.Content.ReadFromJsonAsync<JsonElement>();
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", renewed.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PostAsync("/api/companion/homes/home-a/session/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/companion/homes/home-a/session")).StatusCode);
    }

    [Fact]
    public async Task CookieApprovalRequiresAnIdentityBoundAntiforgeryToken()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        var challenge = (await host.Pairings.StartAsync("Phone"))!;
        host.Client.DefaultRequestHeaders.Add("Cookie", ".lucia.session=synthetic-cookie");
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.PostAsJsonAsync("/api/companion/owner/pairings/review", new { userCode = challenge.UserCode })).StatusCode);
        var csrfResponse = await host.Client.GetAsync("/api/companion/owner/antiforgery");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var cookie = Assert.Single(csrfResponse.Headers.GetValues("Set-Cookie"));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        host.Client.DefaultRequestHeaders.Remove("Cookie");
        host.Client.DefaultRequestHeaders.Add("Cookie", ".lucia.session=synthetic-cookie; " + cookie.Split(';')[0]);
        host.Client.DefaultRequestHeaders.Add(csrf.GetProperty("headerName").GetString()!, csrf.GetProperty("requestToken").GetString());
        var reviewedResponse = await host.Client.PostAsJsonAsync("/api/companion/owner/pairings/review", new { userCode = challenge.UserCode });
        Assert.Equal(HttpStatusCode.OK, reviewedResponse.StatusCode);
        var reviewId = (await reviewedResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        var approval = new { codeConfirmed = true, grant = new { homeId = "home-a", memberId = "member-a", places = Array.Empty<object>() } };
        var headerName = csrf.GetProperty("headerName").GetString()!;
        host.Client.DefaultRequestHeaders.Remove(headerName);
        host.Client.DefaultRequestHeaders.Add(headerName, "invalid-proof");
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.PostAsJsonAsync($"/api/companion/owner/pairings/{reviewId}/approve", approval)).StatusCode);
        Assert.Equal("Pending", (await host.Pairings.ReviewAsync(challenge.UserCode))!.Status);
        host.Client.DefaultRequestHeaders.Remove(headerName);
        host.Client.DefaultRequestHeaders.Add(headerName, csrf.GetProperty("requestToken").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PostAsJsonAsync($"/api/companion/owner/pairings/{reviewId}/approve", approval)).StatusCode);
    }

    [Fact]
    public async Task NativeAndInternalCredentialsCannotApproveAndOversizedBodiesAreRejected()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        var issued = await host.Sessions.CreateAsync(new("home-a", "member-a", [], canReadEngine: true), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", issued.AccessToken);
        host.Client.DefaultRequestHeaders.Add("Cookie", ".lucia.session=synthetic-cookie");
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/companion/owner/sessions")).StatusCode);
        host.Client.DefaultRequestHeaders.Remove("Cookie");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", "synthetic-internal");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/companion/owner/sessions")).StatusCode);
        host.Client.DefaultRequestHeaders.Authorization = null;
        var large = await host.Client.PostAsJsonAsync("/api/companion/pairings", new { deviceName = new string('x', 20_000) });
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, large.StatusCode);
    }

    [Fact]
    public async Task AnonymousIntentCreationIsRateLimited()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        for (var index = 0; index < 5; index++)
        {
            Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsJsonAsync("/api/companion/pairings", new { deviceName = "Phone" })).StatusCode);
        }
        var rejected = await host.Client.PostAsJsonAsync("/api/companion/pairings", new { deviceName = "Phone" });
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task MissingCurrentOwnerAuthorityOverridesCachedAuthentication()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/companion/owner/sessions")).StatusCode);
        // ValidateKeyAsync still authenticates the owner, as a stale validation cache could.
        A.CallTo(() => host.Keys.GetKeyAsync("owner-a", A<CancellationToken>._))
            .Returns(Task.FromResult<lucia.Agents.Auth.ApiKeySummary?>(null));
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/companion/owner/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.PostAsJsonAsync("/api/companion/owner/pairings/review", new { userCode = "00000-00000" })).StatusCode);
    }

    [Fact]
    public async Task InvalidApprovalsCannotChangeIntentAndOwnerCanCancelOrRevokeDevices()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        var challenge = (await host.Pairings.StartAsync("Phone"))!;
        var review = (await host.Pairings.ReviewAsync(challenge.UserCode))!;
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        foreach (var payload in new[]
        {
            new { codeConfirmed = false, grant = new { homeId = "home-a", memberId = "member-a", places = new[] { new { placeId = "living", fields = new[] { "Temperature" } } } } },
            new { codeConfirmed = true, grant = new { homeId = "other-home", memberId = "member-a", places = new[] { new { placeId = "living", fields = new[] { "Temperature" } } } } },
            new { codeConfirmed = true, grant = new { homeId = "home-a", memberId = "member-a", places = new[] { new { placeId = "living", fields = new[] { "UnknownField" } } } } }
        })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync($"/api/companion/owner/pairings/{review.Id}/approve", payload)).StatusCode);
            Assert.Equal("Pending", (await host.Pairings.ReviewAsync(challenge.UserCode))!.Status);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PostAsync($"/api/companion/owner/pairings/{review.Id}/cancel", null)).StatusCode);
        Assert.Equal(CompanionPairingStatus.Denied, (await host.Pairings.RedeemAsync(challenge.DeviceCode)).Status);
        var issued = await host.Sessions.CreateAsync(new("home-a", "member-a", []), "Tablet");
        var list = await host.Client.GetStringAsync("/api/companion/owner/sessions");
        Assert.Contains(issued.SessionId, list);
        Assert.DoesNotContain(issued.AccessToken, list);
        Assert.DoesNotContain(issued.RefreshToken, list);
        Assert.Equal(HttpStatusCode.NoContent, (await host.Client.DeleteAsync($"/api/companion/owner/sessions/{issued.SessionId}")).StatusCode);
        Assert.Null(await host.Sessions.AuthenticateAsync(issued.AccessToken));
        Assert.Null(await host.Sessions.RenewAsync(issued.RefreshToken));
        Assert.Equal("[]", await host.Client.GetStringAsync("/api/companion/owner/sessions"));
    }
}

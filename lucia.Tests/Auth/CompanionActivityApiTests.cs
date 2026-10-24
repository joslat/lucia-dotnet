using System.Net.Http.Json;
using System.Text.Json;
using FakeItEasy;
using lucia.AgentHost.Companion;
using lucia.Agents.Companion;

namespace lucia.Tests.Auth;

public sealed class CompanionActivityApiTests
{
    [Fact]
    public async Task TargetProjectionShowsOnlyCurrentAuthorizedAreasAndNeverItsPrivateEntitySelectors()
    {
        await using var host = await StartAsync();
        A.CallTo(() => host.HomeAssistant.GetAreaRegistryAsync(A<CancellationToken>._)).Returns([
            new() { AreaId = "porch", Name = "Porch" }, new() { AreaId = "bedroom", Name = "PRIVATE_BEDROOM" }]);
        A.CallTo(() => host.HomeAssistant.GetEntityRegistryAsync(A<CancellationToken>._)).Returns([
            new() { EntityId = "light.porch", AreaId = "porch" }, new() { EntityId = "light.private", AreaId = "bedroom" }]);
        using var request = host.Activity.BeginOwner("owner-a"); request!.Working();
        request.ReportRequestedTargets(["light.porch", "light.private", "light.missing"]);
        var grant = new CompanionGrant("home-a", "member-a", [new("porch", [CompanionField.Temperature])], canReadActivity: true);
        var issued = await host.Sessions.CreateAsync(grant, "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", issued.AccessToken);
        var snapshot = await host.Client.GetFromJsonAsync<JsonElement>("/api/companion/homes/home-a/activity");
        var areas = Assert.Single(snapshot.GetProperty("requests").EnumerateArray()).GetProperty("currentTargetAreas");
        Assert.True(areas.GetProperty("isPartial").GetBoolean());
        Assert.Equal("Porch", Assert.Single(areas.GetProperty("places").EnumerateArray()).GetProperty("name").GetString());
        Assert.DoesNotContain("PRIVATE_", snapshot.ToString()); Assert.DoesNotContain("light.", snapshot.ToString());
        A.CallTo(() => host.HomeAssistant.GetStatesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task AnUnavailableRegistryDoesNotInventTargetsOrHideTheReportedRequestLifecycle()
    {
        await using var host = await StartAsync();
        A.CallTo(() => host.HomeAssistant.GetAreaRegistryAsync(A<CancellationToken>._)).ThrowsAsync(new InvalidOperationException("PRIVATE_DIAGNOSTIC"));
        using var request = host.Activity.BeginOwner("owner-a"); request!.Working(); request.ReportRequestedTargets(["light.porch"]);
        var issued = await host.Sessions.CreateAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])], canReadActivity: true), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", issued.AccessToken);
        var snapshot = await host.Client.GetFromJsonAsync<JsonElement>("/api/companion/homes/home-a/activity");
        var item = Assert.Single(snapshot.GetProperty("requests").EnumerateArray());
        Assert.Equal("Working", item.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("currentTargetAreas").ValueKind);
        Assert.DoesNotContain("PRIVATE_", snapshot.ToString());
    }

    [Theory]
    [InlineData("place", false, HttpStatusCode.OK)]
    [InlineData("activity", false, HttpStatusCode.Forbidden)]
    [InlineData("revoked", false, HttpStatusCode.Unauthorized)]
    [InlineData("place", true, HttpStatusCode.OK)]
    [InlineData("activity", true, HttpStatusCode.Forbidden)]
    [InlineData("revoked", true, HttpStatusCode.Unauthorized)]
    public async Task AuthorityIsRecheckedAfterSuccessfulOrFailedTargetLookup(string change, bool sourceFails, HttpStatusCode expected)
    {
        const string access = "lca_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var grant = new CompanionGrant("home-a", "member-a", [new("porch", [CompanionField.Temperature])], canReadActivity: true);
        var initial = new CompanionSession(new string('a', 32), "Phone", grant, DateTimeOffset.UtcNow.AddMinutes(5));
        CompanionSession? current = initial;
        var sessions = A.Fake<ICompanionSessionService>();
        A.CallTo(() => sessions.AuthenticateAsync(access, A<CancellationToken>._)).ReturnsLazily(() => current);
        await using var host = await CompanionSecurityTestHost.StartAsync(configureServices: services =>
        {
            services.AddSingleton(sessions);
            services.Configure<CompanionActivityOptions>(settings => { settings.Enabled = true; settings.OwnerMembers["owner-a"] = "member-a"; });
        });
        A.CallTo(() => host.HomeAssistant.GetAreaRegistryAsync(A<CancellationToken>._)).Returns([new() { AreaId = "porch", Name = "PRIVATE_REMOVED_PLACE" }]);
        A.CallTo(() => host.HomeAssistant.GetEntityRegistryAsync(A<CancellationToken>._)).ReturnsLazily(() =>
        {
            current = change == "revoked" ? null : initial with { Grant = new("home-a", "member-a", [], canReadActivity: change != "activity") };
            if (sourceFails) { throw new InvalidOperationException("PRIVATE_SOURCE_FAILURE"); }
            return new lucia.HomeAssistant.Models.EntityRegistryEntry[] { new() { EntityId = "light.porch", AreaId = "porch" } };
        });
        using var request = host.Activity.BeginOwner("owner-a"); request!.Working(); request.ReportRequestedTargets(["light.porch"]);
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", access);
        using var response = await host.Client.GetAsync("/api/companion/homes/home-a/activity");
        Assert.Equal(expected, response.StatusCode);
        Assert.DoesNotContain("PRIVATE_", await response.Content.ReadAsStringAsync());
        if (expected == HttpStatusCode.OK)
        {
            var snapshot = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Null, Assert.Single(snapshot.GetProperty("requests").EnumerateArray()).GetProperty("currentTargetAreas").ValueKind);
        }
        else { Assert.DoesNotContain(request.RequestId, await response.Content.ReadAsStringAsync()); }
    }

    [Fact]
    public async Task LargeTargetProjectionsKeepEveryLifecycleWithinTheWireBudgetAndDiscloseAreaTruncation()
    {
        await using var host = await StartAsync();
        var ids = Enumerable.Range(0, 32).Select(index => $"light.target_{index}").ToArray();
        A.CallTo(() => host.HomeAssistant.GetAreaRegistryAsync(A<CancellationToken>._)).Returns(Enumerable.Range(0, 32)
            .Select(index => new lucia.HomeAssistant.Models.AreaRegistryEntry { AreaId = $"area-{index}", Name = new string('\u4e00', 200) }).ToArray());
        A.CallTo(() => host.HomeAssistant.GetEntityRegistryAsync(A<CancellationToken>._)).Returns(ids.Select((id, index) =>
            new lucia.HomeAssistant.Models.EntityRegistryEntry { EntityId = id, AreaId = $"area-{index}" }).ToArray());
        for (var index = 0; index < 20; index++) { using var request = host.Activity.BeginOwner("owner-a"); request!.ReportRequestedTargets(ids); request.Working(); }
        var grant = new CompanionGrant("home-a", "member-a", Enumerable.Range(0, 32)
            .Select(index => new CompanionPlaceGrant($"area-{index}", [CompanionField.Temperature])).ToArray(), canReadActivity: true);
        var issued = await host.Sessions.CreateAsync(grant, "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", issued.AccessToken);
        using var response = await host.Client.GetAsync("/api/companion/homes/home-a/activity");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync(); Assert.True(bytes.Length <= 65_536);
        var requests = JsonDocument.Parse(bytes).RootElement.GetProperty("requests").EnumerateArray().ToArray();
        Assert.Equal(20, requests.Length);
        Assert.All(requests, item =>
        {
            var areas = item.GetProperty("currentTargetAreas");
            Assert.True(areas.GetProperty("isPartial").GetBoolean());
            Assert.InRange(areas.GetProperty("places").GetArrayLength(), 1, 3);
        });
    }
    [Fact]
    public async Task OnlyTheCurrentMemberSeesItsExplicitlyAttributedLifecycleAndNoPrivateFields()
    {
        await using var host = await StartAsync();
        using var own = host.Activity.BeginOwner("owner-a"); own!.Working();
        using var other = host.Activity.BeginVoice("profile-b"); other!.Complete(CompanionActivityState.ResponseReady);
        var issued = await host.Sessions.CreateAsync(CompanionActivityJournalTests.Grant(), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", issued.AccessToken);
        using var response = await host.Client.GetAsync("/api/companion/homes/home-a/activity");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("Working", text); Assert.Contains(own.RequestId, text);
        foreach (var secret in new[] { other.RequestId, "profile-b", "owner-a", "conversationId", "tool", "transcript", "responseText" })
        { Assert.DoesNotContain(secret, text); }
        var snapshot = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("home-a", snapshot.GetProperty("homeId").GetString());
        Assert.Equal("member-a", snapshot.GetProperty("memberId").GetString());
        Assert.True(snapshot.GetProperty("isConfigured").GetBoolean());
        Assert.Single(snapshot.GetProperty("requests").EnumerateArray());
        // Every client gets its own full snapshot. Reading does not consume events.
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/companion/homes/home-a/activity")).StatusCode);
    }

    [Fact]
    public async Task HomeOnlyEngineAndHistoryGrantsDoNotGrantActivityOrCrossHomeAccess()
    {
        await using var host = await StartAsync();
        using var request = host.Activity.BeginVoice("profile-a");
        foreach (var grant in new[] { new CompanionGrant("home-a", "member-a", []), new("home-a", "member-a", [], canReadEngine: true, canReadHistory: true) })
        {
            var issued = await host.Sessions.CreateAsync(grant, "Phone");
            host.Client.DefaultRequestHeaders.Authorization = new("Bearer", issued.AccessToken);
            using var denied = await host.Client.GetAsync("/api/companion/homes/home-a/activity");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.DoesNotContain(request!.RequestId, await denied.Content.ReadAsStringAsync());
        }
        var activity = await host.Sessions.CreateAsync(CompanionActivityJournalTests.Grant(), "Tablet");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", activity.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/companion/homes/home-b/activity")).StatusCode);
        await host.Sessions.RevokeAsync(activity.SessionId, "home-a");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/companion/homes/home-a/activity")).StatusCode);
    }

    [Fact]
    public async Task BroadOwnerOrInternalCredentialsCannotBecomeANativeActivityIdentity()
    {
        await using var host = await StartAsync();
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        host.Client.DefaultRequestHeaders.Add("Cookie", ".lucia.session=synthetic-cookie");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/companion/homes/home-a/activity")).StatusCode);
    }

    [Fact]
    public async Task DisabledOrUnmappedCollectionIsExplicitNotProofThatNoRequestsExist()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        var issued = await host.Sessions.CreateAsync(CompanionActivityJournalTests.Grant(), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", issued.AccessToken);
        using var response = await host.Client.GetAsync("/api/companion/homes/home-a/activity");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var snapshot = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(snapshot.GetProperty("isConfigured").GetBoolean());
        Assert.Empty(snapshot.GetProperty("requests").EnumerateArray());
    }

    [Theory]
    [InlineData("revoked", HttpStatusCode.Unauthorized)]
    [InlineData("member", HttpStatusCode.Unauthorized)]
    [InlineData("home", HttpStatusCode.Unauthorized)]
    [InlineData("activity", HttpStatusCode.Forbidden)]
    public async Task AuthorityIsCheckedAgainAfterTheSnapshotBeforeAnyDataIsReturned(string change, HttpStatusCode expected)
    {
        const string access = "lca_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var sessions = A.Fake<ICompanionSessionService>();
        var initial = new CompanionSession(new string('a', 32), "Phone", CompanionActivityJournalTests.Grant(), DateTimeOffset.UtcNow.AddMinutes(5));
        CompanionSession? current = change switch
        {
            "revoked" => null,
            "member" => initial with { Grant = CompanionActivityJournalTests.Grant("member-b") },
            "home" => initial with { Grant = new("home-b", "member-a", [], canReadActivity: true) },
            _ => initial with { Grant = new("home-a", "member-a", []) }
        };
        var checks = 0;
        A.CallTo(() => sessions.AuthenticateAsync(access, A<CancellationToken>._)).ReturnsLazily(() => ++checks == 1 ? initial : current);
        await using var host = await CompanionSecurityTestHost.StartAsync(configureServices: services =>
        {
            services.AddSingleton(sessions);
            services.Configure<CompanionActivityOptions>(settings => { settings.Enabled = true; settings.OwnerMembers["owner-a"] = "member-a"; });
        });
        using var request = host.Activity.BeginOwner("owner-a");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", access);
        using var response = await host.Client.GetAsync("/api/companion/homes/home-a/activity");
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(2, checks);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain(request!.RequestId, await response.Content.ReadAsStringAsync());
    }

    private static Task<CompanionSecurityTestHost> StartAsync() => CompanionSecurityTestHost.StartAsync(configureServices: services =>
        services.Configure<CompanionActivityOptions>(settings =>
        {
            settings.Enabled = true;
            settings.VoiceMembers = CompanionActivityJournalTests.Settings().VoiceMembers;
            settings.OwnerMembers = CompanionActivityJournalTests.Settings().OwnerMembers;
        }));
}

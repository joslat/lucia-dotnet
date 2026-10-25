using System.Text.Json;
using FakeItEasy;
using lucia.Agents.Companion;
using lucia.HomeAssistant.Models;

namespace lucia.Tests.Auth;

public sealed class CompanionHistoryApiTests
{
    [Fact]
    public async Task HistoryRequiresItsOwnCapabilityAndExactHomeWithoutTouchingTheSource()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        var tokens = await host.Sessions.CreateAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])]), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync(Query())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync(Query().Replace("home-a", "home-b"))).StatusCode);
        A.CallTo(() => host.HomeAssistant.GetStatesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("")]
    [InlineData("?start=2026-10-24&end=2026-10-25")]
    [InlineData("?start=2026-10-24T12:00:00&end=2026-10-25T12:00:00")]
    [InlineData("?start=invalid&end=invalid")]
    [InlineData("?start=2026-10-24T12:00:00Z&start=2026-10-24T12:00:00Z&end=2026-10-25T12:00:00Z")]
    public async Task InvalidOrAmbiguousWindowsFailBeforeSourceReads(string query)
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        var tokens = await host.Sessions.CreateAsync(Grant(), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/companion/homes/home-a/history" + query)).StatusCode);
        A.CallTo(() => host.HomeAssistant.GetStatesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevocationDuringSuccessOrSourceFailureDiscardsTheResult(bool failure)
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        CompanionHomeApiTests.ConfigureHome(host);
        var tokens = await host.Sessions.CreateAsync(Grant(), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        A.CallTo(() => host.HomeAssistant.GetConfigAsync(A<CancellationToken>._)).ReturnsLazily(async () =>
        {
            await host.Sessions.RevokeAsync(tokens.SessionId, "home-a");
            if (failure) { throw new HttpRequestException("private-upstream-marker"); }
            return Config();
        });
        var response = await host.Client.GetAsync(Query());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("private-upstream-marker", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("revoked", HttpStatusCode.Unauthorized)]
    [InlineData("member", HttpStatusCode.Unauthorized)]
    [InlineData("home", HttpStatusCode.Unauthorized)]
    [InlineData("capability", HttpStatusCode.Forbidden)]
    [InlineData("field", HttpStatusCode.OK)]
    public async Task LateIdentityCapabilityAndFieldChangesApplyBeforeSerialization(string change, HttpStatusCode expected)
    {
        const string access = "lca_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var sessions = A.Fake<ICompanionSessionService>();
        var initial = new CompanionSession(new string('a', 32), "Phone", Grant(), DateTimeOffset.UtcNow.AddMinutes(5));
        CompanionSession? current = change switch
        {
            "revoked" => null,
            "member" => initial with { Grant = new("home-a", "member-b", Grant().Places, canReadHistory: true) },
            "home" => initial with { Grant = new("home-b", "member-a", Grant().Places, canReadHistory: true) },
            "capability" => initial with { Grant = new("home-a", "member-a", Grant().Places) },
            _ => initial with { Grant = new("home-a", "member-a", [new("porch", [CompanionField.Humidity])], canReadHistory: true) }
        };
        var calls = 0;
        A.CallTo(() => sessions.AuthenticateAsync(access, A<CancellationToken>._)).ReturnsLazily(() => ++calls == 1 ? initial : current);
        await using var host = await CompanionSecurityTestHost.StartAsync(configureServices: services => services.AddSingleton(sessions));
        CompanionHomeApiTests.ConfigureHome(host);
        A.CallTo(() => host.HomeAssistant.GetConfigAsync(A<CancellationToken>._)).Returns(Config());
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", access);
        var response = await host.Client.GetAsync(Query());
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(2, calls);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("sensor.porch_temperature", body);
        if (change == "field")
        {
            using var json = JsonDocument.Parse(body);
            Assert.Empty(json.RootElement.GetProperty("tracks").EnumerateArray());
        }
    }

    [Fact]
    public async Task SourceFailureIsSafeAndBroadOwnerCredentialsCannotReadNativeHistory()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync(Query())).StatusCode);
        host.Client.DefaultRequestHeaders.Remove("X-API-Key");
        var tokens = await host.Sessions.CreateAsync(Grant(), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        CompanionHomeApiTests.ConfigureHome(host);
        A.CallTo(() => host.HomeAssistant.GetConfigAsync(A<CancellationToken>._)).ThrowsAsync(new HttpRequestException("private-token-marker"));
        var failed = await host.Client.GetAsync(Query());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.DoesNotContain("private-token-marker", await failed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ActualHttpsSqliteAndHaHistoryProtocolReturnsOnlyTypedAuthorizedSamples()
    {
        await using var source = await SyntheticCompanionHomeSource.StartAsync();
        await using var host = await CompanionSecurityTestHost.StartAsync(homeSource: source.Client, sourceBaseUrl: source.Origin);
        var end = DateTimeOffset.UtcNow.AddMinutes(-1);
        var start = end.AddDays(-1);
        source.HistoryJson = JsonSerializer.Serialize(new[] { new[] { new
        {
            entity_id = "sensor.porch_temperature", state = "20.5", last_updated = start.AddHours(1), last_changed = start.AddHours(1),
            last_reported = end, attributes = new { device_class = "temperature", unit_of_measurement = "°C", secret = "private-history-marker" },
            context = new { user_id = "private-history-marker" }
        } } });
        var tokens = await host.Sessions.CreateAsync(Grant(), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        var response = await host.Client.GetAsync(Query(start, end));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private-", body);
        Assert.DoesNotContain("bedroom", body);
        Assert.DoesNotContain("attributes", body);
        Assert.DoesNotContain("context", body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Europe/Zurich", json.RootElement.GetProperty("timeZone").GetString());
        var track = Assert.Single(json.RootElement.GetProperty("tracks").EnumerateArray());
        Assert.Equal("porch", track.GetProperty("currentPlaceId").GetString());
        var sample = Assert.Single(track.GetProperty("samples").EnumerateArray());
        Assert.Equal("Recorded", sample.GetProperty("quality").GetString());
        Assert.Equal(20.5, sample.GetProperty("numberValue").GetDouble());
        Assert.Contains("history:sensor.porch_temperature", source.Reads);
    }

    private static CompanionGrant Grant() => new("home-a", "member-a", [new("porch", [CompanionField.Temperature])], canReadHistory: true);
    internal static ConfigResponse Config() => new()
    { TimeZone = "Europe/Zurich", Components = ["history", "recorder"], ConfigDir = "/private-marker", Elevation = 0, Latitude = 0, Longitude = 0,
        LocationName = "private-marker", UnitSystem = null!, Version = "synthetic" };
    private static string Query(DateTimeOffset? start = null, DateTimeOffset? end = null)
    {
        var until = end ?? DateTimeOffset.UtcNow.AddMinutes(-1);
        return "/api/companion/homes/home-a/history?start=" + Uri.EscapeDataString((start ?? until.AddDays(-1)).ToString("O"))
            + "&end=" + Uri.EscapeDataString(until.ToString("O"));
    }
}

using System.Text;
using System.Text.Json;
using FakeItEasy;
using lucia.Agents.Companion;
using lucia.Agents.Auth;
using lucia.HomeAssistant.Models;
using Microsoft.Data.Sqlite;

namespace lucia.Tests.Auth;

public sealed class CompanionLayoutApiRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnerRevokedDuringSourceQualificationCannotSaveOrReceivePrivateFailure(bool sourceFails)
    {
        await using var host = await CompanionSecurityTestHost.StartAsync(); CompanionHomeApiTests.ConfigureHome(host);
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<HomeAssistantState[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => host.HomeAssistant.GetStatesAsync(A<CancellationToken>._)).ReturnsLazily(() => { entered.TrySetResult(); return release.Task; });
        try
        {
            var pending = CompanionLayoutApiTests.PutAsync(host, CompanionLayoutApiTests.Layout(), null);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            A.CallTo(() => host.Keys.GetKeyAsync("owner-a", A<CancellationToken>._)).Returns((ApiKeySummary?)null);
            if (sourceFails) { release.TrySetException(new HttpRequestException("PRIVATE_SOURCE_FAILURE")); } else { release.TrySetResult([]); }
            using var response = await pending;
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.DoesNotContain("PRIVATE", await response.Content.ReadAsStringAsync());
            Assert.Null(await host.Layouts.ReadAsync("home-a"));
        }
        finally { release.TrySetResult([]); }
    }

    [Theory]
    [InlineData("revoked", false, HttpStatusCode.Unauthorized)]
    [InlineData("revoked", true, HttpStatusCode.Unauthorized)]
    [InlineData("member", false, HttpStatusCode.Unauthorized)]
    [InlineData("member", true, HttpStatusCode.Unauthorized)]
    [InlineData("home", false, HttpStatusCode.Unauthorized)]
    [InlineData("session", false, HttpStatusCode.Unauthorized)]
    [InlineData("place", false, HttpStatusCode.NotFound)]
    [InlineData("place", true, HttpStatusCode.NotFound)]
    [InlineData("field", false, HttpStatusCode.OK)]
    [InlineData("field", true, HttpStatusCode.ServiceUnavailable)]
    public async Task LateSessionAndGrantChangesDiscardOrRestrictQualifiedData(string change, bool sourceFails, HttpStatusCode expected)
    {
        const string access = "lca_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var sessions = A.Fake<ICompanionSessionService>();
        var initial = new CompanionSession(new string('a', 32), "Phone", new("home-a", "member-a", [new("porch", [CompanionField.Temperature, CompanionField.Humidity])]), DateTimeOffset.UtcNow.AddMinutes(5));
        CompanionSession? current = initial;
        A.CallTo(() => sessions.AuthenticateAsync(access, A<CancellationToken>._)).ReturnsLazily(() => current);
        await using var host = await CompanionSecurityTestHost.StartAsync(configureServices: services => services.AddSingleton(sessions));
        CompanionHomeApiTests.ConfigureHome(host); Assert.NotNull(await host.Layouts.TrySaveAsync(CompanionLayoutApiTests.Layout(), null, "owner-a"));
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", access);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<HomeAssistantState[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => host.HomeAssistant.GetStatesAsync(A<CancellationToken>._)).ReturnsLazily(() => { entered.TrySetResult(); return release.Task; });
        try
        {
            var pending = host.Client.GetAsync("/api/companion/homes/home-a/layout"); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            current = change switch
            {
                "revoked" => null,
                "session" => initial with { Id = new string('b', 32) },
                "member" => initial with { Grant = new("home-a", "member-b", initial.Grant.Places) },
                "home" => initial with { Grant = new("home-b", "member-a", initial.Grant.Places) },
                "place" => initial with { Grant = new("home-a", "member-a", []) },
                _ => initial with { Grant = new("home-a", "member-a", [new("porch", [CompanionField.Humidity])]) }
            };
            if (sourceFails) { release.TrySetException(new HttpRequestException("PRIVATE_SOURCE_FAILURE")); }
            else { release.TrySetResult([CompanionHomeApiTests.State("sensor.porch_temperature", "21.5", "temperature", "°C"), CompanionHomeApiTests.State("sensor.porch_humidity", "89", "humidity", "%")]); }
            using var response = await pending; Assert.Equal(expected, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("PRIVATE", body); Assert.DoesNotContain("sensor.porch_temperature", body);
            if (expected == HttpStatusCode.OK)
            {
                var data = JsonDocument.Parse(body).RootElement;
                Assert.Equal("Humidity", Assert.Single(data.GetProperty("bindings").EnumerateArray()).GetProperty("field").GetString());
            }
        }
        finally { release.TrySetResult([]); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StoreFailureStillRechecksCurrentOwnerWithoutLeakingDiagnostics(bool revoke)
    {
        var store = A.Fake<ICompanionLayoutStore>();
        await using var host = await CompanionSecurityTestHost.StartAsync(configureServices: services => services.AddSingleton(store));
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        A.CallTo(() => store.ReadAsync("home-a", A<CancellationToken>._)).Invokes(() =>
        { if (revoke) { A.CallTo(() => host.Keys.GetKeyAsync("owner-a", A<CancellationToken>._)).Returns((ApiKeySummary?)null); } })
            .ThrowsAsync(new SqliteException("PRIVATE_DATABASE_FAILURE", 14));
        using var response = await host.Client.GetAsync("/api/companion/owner/layout");
        Assert.Equal(revoke ? HttpStatusCode.Forbidden : HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("PRIVATE", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanonicalOwnerPutAllowsBoundedLargeBodiesButCredentialsKeepTheirSmallBudget(bool chunked)
    {
        await using var host = await CompanionSecurityTestHost.StartAsync(); CompanionHomeApiTests.ConfigureHome(host);
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        var layout = JsonSerializer.Serialize(CompanionLayoutApiTests.Layout(), CompanionLayoutJson.Options);
        var valid = new string(' ', 20_000) + "{\"expectedRevision\":null,\"layout\":" + layout + "}";
        using var request = Raw(HttpMethod.Put, "/api/companion/owner/layout", valid, chunked);
        using var accepted = await host.Client.SendAsync(request); Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var oversize = Raw(HttpMethod.Put, "/api/companion/owner/layout", new string(' ', 524_289), chunked);
        using var rejected = await host.Client.SendAsync(oversize); Assert.Equal(HttpStatusCode.RequestEntityTooLarge, rejected.StatusCode);
        using var credential = Raw(HttpMethod.Post, "/api/companion/pairings", new string(' ', 20_000) + "{\"deviceName\":\"Phone\"}", chunked);
        using var stillSmall = await host.Client.SendAsync(credential); Assert.Equal(HttpStatusCode.RequestEntityTooLarge, stillSmall.StatusCode);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("missing")]
    [InlineData("numeric-field")]
    public async Task MalformedOwnerMetadataDoesNotWriteOrEchoTheBody(string defect)
    {
        await using var host = await CompanionSecurityTestHost.StartAsync(); CompanionHomeApiTests.ConfigureHome(host);
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        var layout = JsonSerializer.Serialize(CompanionLayoutApiTests.Layout("PRIVATE_BODY"), CompanionLayoutJson.Options);
        var json = "{\"expectedRevision\":null,\"layout\":" + layout + "}";
        json = defect switch
        {
            "duplicate" => json.Replace("\"name\":\"PRIVATE_BODY\"", "\"name\":\"PRIVATE_BODY\",\"name\":\"Other\"", StringComparison.Ordinal),
            "unknown" => json.Replace("\"expectedRevision\":null", "\"expectedRevision\":null,\"ownerId\":\"forged\"", StringComparison.Ordinal),
            "missing" => json.Replace("\"expectedRevision\":null,", "", StringComparison.Ordinal),
            _ => json.Replace("\"field\":\"Temperature\"", "\"field\":0", StringComparison.Ordinal)
        };
        using var request = Raw(HttpMethod.Put, "/api/companion/owner/layout", json, false);
        using var response = await host.Client.SendAsync(request); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("PRIVATE_BODY", await response.Content.ReadAsStringAsync()); Assert.Null(await host.Layouts.ReadAsync("home-a"));
    }

    private static HttpRequestMessage Raw(HttpMethod method, string route, string body, bool chunked)
    {
        var request = new HttpRequestMessage(method, route) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (chunked) { request.Headers.TransferEncodingChunked = true; }
        return request;
    }

    [Fact]
    public async Task SourceBindingChangeDuringOwnerValidationDoesNotWriteAnotherHomesLayout()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync(); CompanionHomeApiTests.ConfigureHome(host);
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        A.CallTo(() => host.HomeAssistant.GetStatesAsync(A<CancellationToken>._)).Invokes(() => host.SourceSettings.BaseUrl = "http://another-home.invalid")
            .Returns([CompanionHomeApiTests.State("sensor.porch_temperature", "21.5", "temperature", "°C")]);
        using var response = await CompanionLayoutApiTests.PutAsync(host, CompanionLayoutApiTests.Layout(), null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode); Assert.Null(await host.Layouts.ReadAsync("home-a"));
        Assert.DoesNotContain("another-home", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MemberStoreFailureAlsoRechecksRevocationBeforeReturningASafeFailure(bool revoke)
    {
        const string access = "lca_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var sessions = A.Fake<ICompanionSessionService>(); var store = A.Fake<ICompanionLayoutStore>();
        CompanionSession? current = new(new string('a', 32), "Phone", new("home-a", "member-a", [new("porch", [CompanionField.Temperature])]), DateTimeOffset.UtcNow.AddMinutes(5));
        A.CallTo(() => sessions.AuthenticateAsync(access, A<CancellationToken>._)).ReturnsLazily(() => current);
        A.CallTo(() => store.ReadAsync("home-a", A<CancellationToken>._)).Invokes(() => { if (revoke) { current = null; } })
            .ThrowsAsync(new SqliteException("PRIVATE_DATABASE_FAILURE", 14));
        await using var host = await CompanionSecurityTestHost.StartAsync(configureServices: services => { services.AddSingleton(sessions); services.AddSingleton(store); });
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", access);
        using var response = await host.Client.GetAsync("/api/companion/homes/home-a/layout");
        Assert.Equal(revoke ? HttpStatusCode.Unauthorized : HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("PRIVATE", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriteStoreFailureDoesNotSkipTheLastOwnerCheck(bool revoke)
    {
        var store = A.Fake<ICompanionLayoutStore>();
        await using var host = await CompanionSecurityTestHost.StartAsync(configureServices: services => services.AddSingleton(store));
        CompanionHomeApiTests.ConfigureHome(host); host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        A.CallTo(() => store.TrySaveAsync(A<CompanionLayout>._, A<string?>._, "owner-a", A<CancellationToken>._)).Invokes(() =>
        { if (revoke) { A.CallTo(() => host.Keys.GetKeyAsync("owner-a", A<CancellationToken>._)).Returns((ApiKeySummary?)null); } })
            .ThrowsAsync(new SqliteException("PRIVATE_SAVE_FAILURE", 14));
        using var response = await CompanionLayoutApiTests.PutAsync(host, CompanionLayoutApiTests.Layout(), null);
        Assert.Equal(revoke ? HttpStatusCode.Forbidden : HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("PRIVATE", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnauthorizedLargeOwnerBodiesAreRejectedBeforeParsingOrSourceCalls(bool nativeWithOwnerCookie)
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        if (nativeWithOwnerCookie)
        {
            var issued = await host.Sessions.CreateAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])]), "Phone");
            host.Client.DefaultRequestHeaders.Authorization = new("Bearer", issued.AccessToken);
            host.Client.DefaultRequestHeaders.Add("Cookie", ".lucia.session=synthetic-cookie");
        }
        using var request = Raw(HttpMethod.Put, "/api/companion/owner/layout", new string('x', 20_000), false);
        using var response = await host.Client.SendAsync(request);
        Assert.Equal(nativeWithOwnerCookie ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized, response.StatusCode);
        A.CallTo(() => host.HomeAssistant.GetStatesAsync(A<CancellationToken>._)).MustNotHaveHappened();
        Assert.Null(await host.Layouts.ReadAsync("home-a"));
    }
}

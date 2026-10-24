using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Lucia.Companion.Connection;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class CompanionActivityClientTests
{
    private const string Access = "lca_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    [Fact]
    public async Task TypedActivityUsesOnlyTheVerifiedHomeMemberAndExplicitCapability()
    {
        using var api = Client(Payload(), request =>
        {
            Assert.Equal("/api/companion/homes/home-a/activity", request.RequestUri!.AbsolutePath);
            Assert.Equal(Access, request.Headers.Authorization!.Parameter);
            Assert.False(request.Headers.Contains("Cookie"));
        });
        var snapshot = await api.GetActivityAsync(Access, Session(), TestContext.Current.CancellationToken);
        Assert.Equal("member-a", snapshot.MemberId);
        Assert.Equal(RequestActivityState.Working, Assert.Single(snapshot.Requests).State);
        Assert.True(snapshot.IsConfigured);
    }

    [Theory]
    [InlineData("home")]
    [InlineData("member")]
    [InlineData("unknown-state")]
    [InlineData("integer-state")]
    [InlineData("duplicate")]
    [InlineData("invalid-id")]
    [InlineData("reversed-time")]
    [InlineData("future-time")]
    [InlineData("coverage")]
    [InlineData("old-update")]
    [InlineData("revision")]
    [InlineData("capacity")]
    [InlineData("disabled-with-data")]
    [InlineData("false-more")]
    [InlineData("invalid-previous")]
    [InlineData("self-previous")]
    public async Task MalformedActivityIsRejectedWithoutPrivateDiagnosticBodies(string defect)
    {
        var body = Payload(); var item = body["requests"]![0]!;
        switch (defect)
        {
            case "home": body["homeId"] = "home-b"; break;
            case "member": body["memberId"] = "member-b"; break;
            case "unknown-state": item["state"] = "PRIVATE_MARKER"; break;
            case "integer-state": item["state"] = 1; break;
            case "duplicate": body["requests"]!.AsArray().Add(item.DeepClone()); break;
            case "invalid-id": item["requestId"] = "PRIVATE_MARKER"; break;
            case "reversed-time": item["startedAt"] = "2026-10-24T12:00:00Z"; break;
            case "future-time": item["updatedAt"] = "2026-10-24T12:00:01Z"; break;
            case "coverage": body["coverageStartedAt"] = "2026-10-24T12:00:01Z"; break;
            case "old-update": item["state"] = "ResponseReady"; item["startedAt"] = "2026-10-24T10:00:00Z"; item["updatedAt"] = "2026-10-24T10:00:01Z"; break;
            case "revision": item["revision"] = 0; break;
            case "capacity": for (var i = 0; i < 20; i++) { var next = item.DeepClone(); next["requestId"] = i.ToString("x32"); body["requests"]!.AsArray().Add(next); } break;
            case "disabled-with-data": body["isConfigured"] = false; break;
            case "false-more": body["hasMore"] = true; break;
            case "invalid-previous": item["previousRequestId"] = "PRIVATE_MARKER"; break;
            case "self-previous": item["previousRequestId"] = item["requestId"]!.GetValue<string>(); break;
        }
        using var api = Client(body);
        var error = await Assert.ThrowsAsync<CompanionConnectionException>(() => api.GetActivityAsync(Access, Session(), TestContext.Current.CancellationToken));
        Assert.Equal(ConnectionFailure.InvalidResponse, error.Failure);
        Assert.DoesNotContain("PRIVATE_MARKER", error.ToString());
    }

    [Fact]
    public async Task AHomeOnlyGrantSendsNoActivityRequest()
    {
        using var api = new CompanionApiClient(new Uri("https://synthetic.invalid"), new ScriptedCompanionHandler((_, _) => throw new InvalidOperationException("No activity authority.")));
        await Assert.ThrowsAsync<ArgumentException>(() => api.GetActivityAsync(Access, CompanionHomeClientTests.Grant(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ActivityKeepsThe64KiBResponseLimit()
    {
        using var api = new CompanionApiClient(new Uri("https://synthetic.invalid"), new ScriptedCompanionHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 65_537), Encoding.UTF8, "application/json") })));
        Assert.Equal(ConnectionFailure.InvalidResponse, (await Assert.ThrowsAsync<CompanionConnectionException>(() =>
            api.GetActivityAsync(Access, Session(), TestContext.Current.CancellationToken))).Failure);
    }

    [Fact]
    public async Task AnOngoingRequestRetainsItsOldReportedStatusTimeWithoutBecomingRecentHistory()
    {
        var body = Payload(); var request = body["requests"]![0]!;
        request["startedAt"] = "2026-10-24T10:00:00Z"; request["updatedAt"] = "2026-10-24T10:00:01Z";
        using var api = Client(body);
        var snapshot = await api.GetActivityAsync(Access, Session(), TestContext.Current.CancellationToken);
        Assert.Equal(RequestActivityState.Working, Assert.Single(snapshot.Requests).State);
        Assert.True(snapshot.Requests[0].UpdatedAt < snapshot.CoverageStartedAt);
    }

    [Fact]
    public async Task AContinuedResponseCarriesOnlyABoundedOpaquePreviousRequestLink()
    {
        var body = Payload(); var item = body["requests"]![0]!;
        item["state"] = "Continued"; item["previousRequestId"] = new string('b', 32);
        using var api = Client(body);
        var request = Assert.Single((await api.GetActivityAsync(Access, Session(), TestContext.Current.CancellationToken)).Requests);
        Assert.Equal("Continued", request.State.ToString());
        Assert.Equal(new string('b', 32), request.PreviousRequestId);
        // A previous turn may be outside the bounded current snapshot. Do not invent or require its history.
    }

    internal static DeviceSession Session() => new(new string('a', 32), "Phone", DateTimeOffset.UtcNow.AddDays(1), "home-a", "member-a", [], canReadActivity: true);
    internal static JsonObject Payload() => JsonNode.Parse("""
        {"homeId":"home-a","memberId":"member-a","capturedAt":"2026-10-24T12:00:00Z","coverageStartedAt":"2026-10-24T11:30:00Z",
         "isConfigured":true,"hasGap":false,"hasMore":false,"requests":[{"requestId":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
         "state":"Working","startedAt":"2026-10-24T11:59:58Z","updatedAt":"2026-10-24T11:59:59Z","revision":2}]}
        """)!.AsObject();
    private static CompanionApiClient Client(JsonNode payload, Action<HttpRequestMessage>? check = null) => new(new Uri("https://synthetic.invalid"),
        new ScriptedCompanionHandler((request, _) =>
        {
            check?.Invoke(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json") });
        }));
}

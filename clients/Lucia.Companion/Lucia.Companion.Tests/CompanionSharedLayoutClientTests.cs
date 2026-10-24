using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class CompanionSharedLayoutClientTests
{
    internal const string Access = "lca_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task MemberLayoutUsesOnlyTheNativeHomeRouteAndContainsNoOwnerDocument()
    {
        using var api = Client(Payload().ToJsonString(), request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://localhost/api/companion/homes/home-a/layout", request.RequestUri!.AbsoluteUri);
            Assert.Equal(Access, request.Headers.Authorization!.Parameter);
            Assert.False(request.Headers.Contains("Cookie"));
        });
        var layout = Assert.IsType<SharedHomeLayout>(await api.GetSharedLayoutAsync(Access, Grant(), TestContext.Current.CancellationToken));
        Assert.Equal("Our home", layout.Name);
        Assert.Equal("room-a", Assert.Single(layout.Places).Id);
        Assert.Equal(PlaceIcon.Door, layout.Places[0].Icon);
        Assert.Equal(ObservationRole.Contact, Assert.Single(layout.Bindings).Field);
        Assert.True(layout.HasUnqualifiedBindings);
        Assert.DoesNotContain(typeof(SharedHomeLayout).GetProperties(), property => property.Name is "SourceBaseUrl" or "UpdatedBy" or "UpdatedAt" or "Readings");
    }

    [Theory]
    [InlineData("home")]
    [InlineData("member")]
    [InlineData("revision")]
    [InlineData("time")]
    [InlineData("name")]
    [InlineData("whitespace")]
    [InlineData("control")]
    [InlineData("hidden-place")]
    [InlineData("empty-grant")]
    [InlineData("duplicate-place")]
    [InlineData("null-place")]
    [InlineData("place-id")]
    [InlineData("icon")]
    [InlineData("integer-icon")]
    [InlineData("orphan-edge")]
    [InlineData("self-edge")]
    [InlineData("duplicate-edge")]
    [InlineData("null-edge")]
    [InlineData("orphan-binding")]
    [InlineData("moved-binding")]
    [InlineData("duplicate-binding")]
    [InlineData("null-binding")]
    [InlineData("hidden-field")]
    [InlineData("field")]
    [InlineData("integer-field")]
    [InlineData("source-id")]
    [InlineData("owner-field")]
    [InlineData("nested-owner-field")]
    [InlineData("missing-review-flag")]
    [InlineData("missing-bindings")]
    [InlineData("null-places")]
    [InlineData("empty-places")]
    public async Task InvalidOrUnapprovedMetadataCannotReachTheCaller(string defect)
    {
        var payload = Payload(); var place = payload["places"]![0]!; var binding = payload["bindings"]![0]!;
        var grant = Grant();
        switch (defect)
        {
            case "home": payload["homeId"] = "other-home"; break;
            case "member": payload["memberId"] = "other-member"; break;
            case "revision": payload["revision"] = "PRIVATE_MARKER"; break;
            case "time": payload["capturedAt"] = "1970-01-01T00:00:00Z"; break;
            case "name": payload["name"] = new string('a', 201); break;
            case "whitespace": place["name"] = " Porch "; break;
            case "control": place["name"] = "PRIVATE_MARKER\u202e"; break;
            case "hidden-place": place["sourcePlaceId"] = "private-room"; binding["sourcePlaceId"] = "private-room"; break;
            case "empty-grant": grant = Grant([]); break;
            case "duplicate-place": payload["places"]!.AsArray().Add(place.DeepClone()); break;
            case "null-place": payload["places"]!.AsArray().Add((JsonNode?)null); break;
            case "place-id": place["id"] = "PRIVATE_MARKER/room"; break;
            case "icon": place["icon"] = "PRIVATE_MARKER"; break;
            case "integer-icon": place["icon"] = 1; break;
            case "orphan-edge": payload["connections"] = JsonNode.Parse("""[{"fromPlaceId":"room-a","toPlaceId":"private-room"}]"""); break;
            case "self-edge": payload["connections"] = JsonNode.Parse("""[{"fromPlaceId":"room-a","toPlaceId":"room-a"}]"""); break;
            case "duplicate-edge":
                payload["places"]!.AsArray().Add(JsonNode.Parse("""{"id":"room-b","name":"Other porch view","icon":"Home","sourcePlaceId":"porch"}"""));
                payload["connections"] = JsonNode.Parse("""[{"fromPlaceId":"room-a","toPlaceId":"room-b"},{"fromPlaceId":"room-b","toPlaceId":"room-a"}]"""); break;
            case "null-edge": payload["connections"]!.AsArray().Add((JsonNode?)null); break;
            case "orphan-binding": binding["placeId"] = "private-room"; break;
            case "moved-binding": binding["sourcePlaceId"] = "elsewhere"; break;
            case "duplicate-binding": payload["bindings"]!.AsArray().Add(binding.DeepClone()); break;
            case "null-binding": payload["bindings"]!.AsArray().Add((JsonNode?)null); break;
            case "hidden-field": binding["field"] = "Temperature"; break;
            case "field": binding["field"] = "PRIVATE_MARKER"; break;
            case "integer-field": binding["field"] = 1; break;
            case "source-id": binding["sourceId"] = "friendly search term"; break;
            case "owner-field": payload["sourceBaseUrl"] = "http://PRIVATE_MARKER"; break;
            case "nested-owner-field": place["updatedBy"] = "PRIVATE_MARKER"; break;
            case "missing-review-flag": payload.Remove("hasUnqualifiedBindings"); break;
            case "missing-bindings": payload.Remove("bindings"); break;
            case "null-places": payload["places"] = null; break;
            case "empty-places": payload["places"] = new JsonArray(); payload["bindings"] = new JsonArray(); break;
        }
        using var api = Client(payload.ToJsonString());
        var error = await Assert.ThrowsAsync<CompanionConnectionException>(() => api.GetSharedLayoutAsync(Access, grant, TestContext.Current.CancellationToken));
        Assert.Equal(ConnectionFailure.InvalidResponse, error.Failure);
        Assert.DoesNotContain("PRIVATE_MARKER", error.ToString());
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("case-alias")]
    [InlineData("nested-duplicate")]
    [InlineData("oversize")]
    [InlineData("html")]
    public async Task AmbiguousOrOversizedWireDocumentsFailClosed(string defect)
    {
        var payload = Payload().ToJsonString();
        payload = defect switch
        {
            "duplicate" => payload.Insert(1, "\"name\":\"PRIVATE_MARKER\","),
            "case-alias" => payload.Insert(1, "\"Name\":\"PRIVATE_MARKER\","),
            "nested-duplicate" => payload.Replace("\"icon\":", "\"icon\":\"Home\",\"icon\":", StringComparison.Ordinal),
            "oversize" => payload + new string(' ', 524_288),
            _ => payload
        };
        using var api = Client(payload, mediaType: defect == "html" ? "text/html" : "application/json");
        Assert.Equal(ConnectionFailure.InvalidResponse, (await Assert.ThrowsAsync<CompanionConnectionException>(() =>
            api.GetSharedLayoutAsync(Access, Grant(), TestContext.Current.CancellationToken))).Failure);
    }

    [Fact]
    public async Task MissingPermittedViewIsDistinctFromAnEmptyLayoutOrFailure()
    {
        using var api = Client("""{"error":"layout_not_configured"}""", status: HttpStatusCode.NotFound);
        Assert.Null(await api.GetSharedLayoutAsync(Access, Grant(), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"error\":\"different\"}")]
    [InlineData("{\"error\":\"layout_not_configured\",\"sourceBaseUrl\":\"PRIVATE_MARKER\"}")]
    [InlineData("{\"error\":\"layout_not_configured\",\"error\":\"layout_not_configured\"}")]
    public async Task MissingResponseMustBeTheExactMemberContract(string payload)
    {
        using var api = Client(payload, status: HttpStatusCode.NotFound);
        Assert.Equal(ConnectionFailure.InvalidResponse, (await Assert.ThrowsAsync<CompanionConnectionException>(() =>
            api.GetSharedLayoutAsync(Access, Grant(), TestContext.Current.CancellationToken))).Failure);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task DenialsAndSourceFailuresAreNeverMissingLayouts(HttpStatusCode status)
    {
        using var api = Client("PRIVATE_MARKER", status: status);
        var error = await Assert.ThrowsAsync<CompanionConnectionException>(() => api.GetSharedLayoutAsync(Access, Grant(), TestContext.Current.CancellationToken));
        Assert.Equal(status, error.StatusCode);
        Assert.DoesNotContain("PRIVATE_MARKER", error.ToString());
    }

    [Fact]
    public void LayoutCopiesItsCollectionsAndCannotCarryReadingsOrPersonalPreferences()
    {
        var places = new List<SharedLayoutPlace> { new("room-a", "Porch", PlaceIcon.Door, "porch") };
        var bindings = new List<SharedLayoutBinding> { new("room-a", "porch", "binary_sensor.door", ObservationRole.Contact) };
        var edges = new List<SharedLayoutConnection>();
        var layout = new SharedHomeLayout("home-a", "member-a", new string('A', 64), DateTimeOffset.UtcNow, "Our home", false, places, edges, bindings);
        places.Clear(); bindings.Clear(); edges.Add(new("room-a", "missing"));
        Assert.Single(layout.Places); Assert.Single(layout.Bindings); Assert.Empty(layout.Connections);
        Assert.Throws<NotSupportedException>(() => ((IList<SharedLayoutPlace>)layout.Places).Clear());
        Assert.DoesNotContain(typeof(SharedHomeLayout).GetProperties(), property => property.Name is "Readings" or "Pins" or "SectionOrder");
    }

    [Theory]
    [InlineData("places")]
    [InlineData("connections")]
    [InlineData("bindings")]
    [InlineData("invalid-unicode")]
    public void LayoutConstructorRejectsCapacityAndInvalidUnicodeBeforeAnyProjection(string defect)
    {
        IReadOnlyList<SharedLayoutPlace> places = [new("room-a", "Porch", PlaceIcon.Door, "porch")];
        IReadOnlyList<SharedLayoutConnection> edges = [];
        IReadOnlyList<SharedLayoutBinding> bindings = [];
        if (defect == "places") { places = Enumerable.Range(0, 201).Select(index => new SharedLayoutPlace($"room-{index}", "Room", PlaceIcon.Home, "porch")).ToArray(); }
        if (defect == "connections") { edges = Enumerable.Repeat(new SharedLayoutConnection("room-a", "room-b"), 1001).ToArray(); }
        if (defect == "bindings") { bindings = Enumerable.Repeat(new SharedLayoutBinding("room-a", "porch", "binary_sensor.door", ObservationRole.Contact), 2001).ToArray(); }
        Assert.Throws<ArgumentException>(() => new SharedHomeLayout("home-a", "member-a", new string('A', 64), DateTimeOffset.UtcNow,
            defect == "invalid-unicode" ? "Bad\ud800" : "Our home", false, places, edges, bindings));
    }

    [Fact]
    public async Task LargerMetadataUsesItsOwnBudgetAndRetainsValidConnectionsWithoutBorrowingReadings()
    {
        var payload = Payload(); payload["places"] = new JsonArray(); payload["bindings"] = new JsonArray();
        for (var index = 0; index < 200; index++)
        {
            payload["places"]!.AsArray().Add(new JsonObject { ["id"] = $"room-{index}", ["name"] = new string('a', 200), ["icon"] = "Door", ["sourcePlaceId"] = "porch" });
            payload["bindings"]!.AsArray().Add(new JsonObject { ["placeId"] = $"room-{index}", ["sourcePlaceId"] = "porch", ["sourceId"] = $"binary_sensor.door_{index}", ["field"] = "Contact" });
        }
        payload["connections"] = JsonNode.Parse("""[{"fromPlaceId":"room-0","toPlaceId":"room-1"},{"fromPlaceId":"room-1","toPlaceId":"room-2"},{"fromPlaceId":"room-2","toPlaceId":"room-0"}]""");
        Assert.InRange(Encoding.UTF8.GetByteCount(payload.ToJsonString()), 65_537, 524_288);
        using var api = Client(payload.ToJsonString());
        var layout = (await api.GetSharedLayoutAsync(Access, Grant(), TestContext.Current.CancellationToken))!;
        Assert.Equal(200, layout.Places.Count); Assert.Equal(200, layout.Bindings.Count); Assert.Equal(3, layout.Connections.Count);
    }

    [Fact]
    public async Task SharedBodyHasAnIndependentDeadlineAndCallerCancellationIsNotAnInvalidResponse()
    {
        using var stalled = new CompanionApiClient(new Uri("https://localhost"), new ScriptedCompanionHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new WaitingCompanionContent() })), TimeSpan.FromMilliseconds(20));
        Assert.Equal(ConnectionFailure.InvalidResponse, (await Assert.ThrowsAsync<CompanionConnectionException>(() =>
            stalled.GetSharedLayoutAsync(Access, Grant(), TestContext.Current.CancellationToken))).Failure);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var cancelled = new CompanionApiClient(new Uri("https://localhost"), new ScriptedCompanionHandler((_, _) =>
        {
            cancellation.Cancel();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new WaitingCompanionContent() });
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.GetSharedLayoutAsync(Access, Grant(), cancellation.Token));
    }

    internal static DeviceSession Grant(IReadOnlyList<ObservationRole>? fields = null) => new(new string('a', 32), "Phone", DateTimeOffset.UtcNow.AddDays(1),
        "home-a", "member-a", [new("porch", fields ?? [ObservationRole.Contact])]);
    internal static JsonObject Payload() => JsonNode.Parse("""
        {"homeId":"home-a","memberId":"member-a","revision":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
        "capturedAt":"2026-10-25T12:00:00Z","name":"Our home","hasUnqualifiedBindings":true,
        "places":[{"id":"room-a","name":"Porch","icon":"Door","sourcePlaceId":"porch"}],"connections":[],
        "bindings":[{"placeId":"room-a","sourcePlaceId":"porch","sourceId":"binary_sensor.door","field":"Contact"}]}
        """)!.AsObject();
    private static CompanionApiClient Client(string payload, Action<HttpRequestMessage>? check = null, HttpStatusCode status = HttpStatusCode.OK,
        string mediaType = "application/json") => new(new Uri("https://localhost"), new ScriptedCompanionHandler((request, _) =>
        { check?.Invoke(request); return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(payload, Encoding.UTF8, mediaType) }); }));
}

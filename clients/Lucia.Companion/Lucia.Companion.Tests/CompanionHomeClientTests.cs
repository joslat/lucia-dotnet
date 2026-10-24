using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class CompanionHomeClientTests
{
    private const string Access = "lca_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task HomeReadUsesOnlyTheVerifiedHomeAndReturnsTypedReportSemantics()
    {
        using var api = Client(Payload(), request =>
        {
            Assert.Equal("/api/companion/homes/home-a/snapshot", request.RequestUri!.AbsolutePath);
            Assert.Equal(Access, request.Headers.Authorization!.Parameter);
            Assert.False(request.Headers.Contains("Cookie"));
        });
        var result = await api.GetHomeAsync(Access, Grant(), TestContext.Current.CancellationToken);
        Assert.Equal("home-a", result.HomeId);
        var place = Assert.Single(result.Places);
        Assert.Equal("Porch", place.Name);
        Assert.Equal(ObservationRole.Temperature, Assert.Single(place.Sources).Field);
        var reading = Assert.Single(place.Readings);
        Assert.Equal(21.5, reading.NumberValue);
        Assert.Equal(ObservationQuality.Current, reading.Quality);
    }

    [Theory]
    [InlineData("home")]
    [InlineData("place")]
    [InlineData("field")]
    [InlineData("duplicate")]
    [InlineData("orphan")]
    [InlineData("unit")]
    [InlineData("untimed")]
    [InlineData("future")]
    [InlineData("quality")]
    [InlineData("unknown-value")]
    [InlineData("boolean-temperature")]
    [InlineData("stale-current-time")]
    [InlineData("current-old-time")]
    [InlineData("missing-report")]
    [InlineData("empty-age")]
    [InlineData("label-controls")]
    public async Task MalformedOrOverGrantedReportsAreRejectedWithoutDiagnosticBodyLeakage(string defect)
    {
        var payload = Payload();
        var place = payload["places"]![0]!;
        var source = place["sources"]![0]!;
        var reading = place["readings"]![0]!;
        switch (defect)
        {
            case "home": payload["homeId"] = "other-home"; break;
            case "place": place["placeId"] = "other-place"; break;
            case "field": source["field"] = "Humidity"; reading["field"] = "Humidity"; reading["unit"] = "%"; break;
            case "duplicate": place["sources"]!.AsArray().Add(source.DeepClone()); break;
            case "orphan": reading["sourceId"] = "sensor.other"; break;
            case "unit": reading["unit"] = "private-response-marker"; break;
            case "untimed": reading["reportedAt"] = null; break;
            case "future": reading["reportedAt"] = "2026-10-24T12:00:01Z"; break;
            case "quality": reading["quality"] = 1; break;
            case "unknown-value": reading["quality"] = "Unknown"; break;
            case "boolean-temperature": reading["numberValue"] = null; reading["isActive"] = false; reading["unit"] = null; break;
            case "stale-current-time": reading["quality"] = "Stale"; break;
            case "current-old-time": reading["reportedAt"] = "2026-10-24T11:00:00Z"; break;
            case "missing-report": place["readings"]!.AsArray().Clear(); break;
            case "empty-age": payload["maximumObservationAgeSeconds"] = 0; break;
            case "label-controls": source["name"] = "private\u202e-response-marker"; break;
        }
        using var api = Client(payload);
        var error = await Assert.ThrowsAsync<CompanionConnectionException>(() => api.GetHomeAsync(Access, Grant(), TestContext.Current.CancellationToken));
        Assert.Equal(ConnectionFailure.InvalidResponse, error.Failure);
        Assert.DoesNotContain("private-response-marker", error.ToString());
    }

    [Theory]
    [InlineData("Stale", "2026-10-24T11:00:00Z", 21.5)]
    [InlineData("Unknown", null, null)]
    [InlineData("Unavailable", "2026-10-24T11:00:00Z", null)]
    public async Task MissingAndDatedLastKnownReportsRetainTheirQualityWithoutBecomingFalseOrZero(string quality, string? reportedAt, double? value)
    {
        var payload = Payload();
        var reading = payload["places"]![0]!["readings"]![0]!;
        reading["quality"] = quality;
        reading["reportedAt"] = reportedAt;
        reading["numberValue"] = value;
        if (value is null) { reading["unit"] = null; }
        using var api = Client(payload);
        var result = Assert.Single(Assert.Single((await api.GetHomeAsync(Access, Grant(), TestContext.Current.CancellationToken)).Places).Readings);
        Assert.Equal(quality, result.Quality.ToString());
        Assert.Equal(value, result.NumberValue);
        Assert.Null(result.IsActive);
    }

    [Fact]
    public async Task OffContactRemainsFalseNotMissingAndBinaryReportsHaveNoInventedUnit()
    {
        var payload = Payload();
        var place = payload["places"]![0]!;
        place["sources"]![0]!["field"] = "Contact";
        var reading = place["readings"]![0]!;
        reading["field"] = "Contact";
        reading["numberValue"] = null;
        reading["isActive"] = false;
        reading["unit"] = null;
        var grant = new DeviceSession(new string('a', 32), "Phone", DateTimeOffset.UtcNow.AddDays(1), "home-a", "member-a", [new("porch", [ObservationRole.Contact])]);
        using var api = Client(payload);
        var result = Assert.Single(Assert.Single((await api.GetHomeAsync(Access, grant, TestContext.Current.CancellationToken)).Places).Readings);
        Assert.False(result.IsActive);
        Assert.Null(result.NumberValue);
        Assert.Null(result.Unit);
    }

    [Fact]
    public async Task DataHasItsOwn512KiBBudgetWithoutRelaxingCredentialResponses()
    {
        var payload = Payload();
        var place = payload["places"]![0]!;
        var source = place["sources"]![0]!.DeepClone();
        var reading = place["readings"]![0]!.DeepClone();
        place["sources"] = new JsonArray();
        place["readings"] = new JsonArray();
        for (var index = 0; index < 700; index++)
        {
            var nextSource = source.DeepClone();
            var nextReading = reading.DeepClone();
            nextSource["sourceId"] = "sensor.temperature_" + index;
            nextReading["sourceId"] = "sensor.temperature_" + index;
            nextSource["name"] = new string('a', 120);
            place["sources"]!.AsArray().Add(nextSource);
            place["readings"]!.AsArray().Add(nextReading);
        }
        Assert.InRange(Encoding.UTF8.GetByteCount(payload.ToJsonString()), 65_537, 524_288);
        using var api = Client(payload);
        Assert.Equal(700, Assert.Single((await api.GetHomeAsync(Access, Grant(), TestContext.Current.CancellationToken)).Places).Sources.Count);
        using var tooBig = new CompanionApiClient(new Uri("https://synthetic.invalid"), new ScriptedCompanionHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 524_289), Encoding.UTF8, "application/json") })));
        Assert.Equal(ConnectionFailure.InvalidResponse, (await Assert.ThrowsAsync<CompanionConnectionException>(() => tooBig.GetHomeAsync(Access, Grant(), TestContext.Current.CancellationToken))).Failure);
        await Assert.ThrowsAsync<CompanionConnectionException>(() => api.StartPairingAsync("Phone", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DiscoveryReturnsMetadataWithoutReadingContractsAndUsesTheSameGrantBoundary()
    {
        var payload = Payload();
        payload.Remove("maximumObservationAgeSeconds");
        payload["places"]![0]!.AsObject().Remove("readings");
        using var api = Client(payload, request => Assert.EndsWith("/discovery", request.RequestUri!.AbsolutePath));
        var result = await api.GetDiscoveryAsync(Access, Grant(), TestContext.Current.CancellationToken);
        Assert.Equal("Porch", Assert.Single(result.Places).Name);
        Assert.DoesNotContain(result.GetType().GetProperties(), property => property.Name == "Readings");
        payload["places"]![0]!["placeId"] = "other-place";
        using var wrong = Client(payload);
        await Assert.ThrowsAsync<CompanionConnectionException>(() => wrong.GetDiscoveryAsync(Access, Grant(), TestContext.Current.CancellationToken));
    }

    internal static DeviceSession Grant() => new(new string('a', 32), "Phone", DateTimeOffset.UtcNow.AddDays(1), "home-a", "member-a", [new("porch", [ObservationRole.Temperature])]);
    internal static JsonObject Payload() => JsonNode.Parse("""
        {"homeId":"home-a","capturedAt":"2026-10-24T12:00:00Z","maximumObservationAgeSeconds":1800,
         "places":[{"placeId":"porch","name":"Porch","sources":[{"sourceId":"sensor.temperature","name":"Temperature","field":"Temperature"}],
         "readings":[{"sourceId":"sensor.temperature","field":"Temperature","quality":"Current","reportedAt":"2026-10-24T12:00:00Z","numberValue":21.5,"isActive":null,"unit":"°C"}]}]}
        """)!.AsObject();
    private static CompanionApiClient Client(JsonNode payload, Action<HttpRequestMessage>? check = null) => new(new Uri("https://synthetic.invalid"),
        new ScriptedCompanionHandler((request, _) =>
        {
            check?.Invoke(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json") });
        }));
}

using System.Net.Http.Json;
using System.Text.Json;
using lucia.Agents.Companion;

namespace lucia.Tests.Auth;

public sealed class CompanionHomeWireTests
{
    [Fact]
    public async Task ActualRestAndRegistryWebSocketsProduceOnlyTheAuthorizedTypedHome()
    {
        await using var source = await SyntheticCompanionHomeSource.StartAsync();
        Assert.Equal("parent", (await source.Client.GetDeviceRegistryAsync(CancellationToken.None))[1].ParentDeviceId);
        await using var host = await CompanionSecurityTestHost.StartAsync(homeSource: source.Client, sourceBaseUrl: source.Origin);
        var tokens = await host.Sessions.CreateAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])]), "Synthetic phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        var response = await host.Client.GetAsync("/api/companion/homes/home-a/snapshot");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private-wire", body);
        Assert.DoesNotContain("connections", body);
        Assert.DoesNotContain("bedroom", body);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        var place = Assert.Single(data.GetProperty("places").EnumerateArray());
        Assert.Equal("porch", place.GetProperty("placeId").GetString());
        Assert.Equal("Porch temperature", Assert.Single(place.GetProperty("sources").EnumerateArray()).GetProperty("name").GetString());
        var reading = Assert.Single(place.GetProperty("readings").EnumerateArray());
        Assert.Equal("Current", reading.GetProperty("quality").GetString());
        Assert.Equal(21.5, reading.GetProperty("numberValue").GetDouble());
        Assert.Equal(source.ReportedAt, reading.GetProperty("reportedAt").GetDateTimeOffset());
        Assert.Equal(new[] { "config/area_registry/list", "config/device_registry/list", "config/entity_registry/list", "states" }, source.Reads.Distinct().Order(StringComparer.Ordinal));
    }
}

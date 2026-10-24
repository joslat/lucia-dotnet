using System.Net.Http.Json;
using System.Text.Json;
using lucia.Agents.Companion;

namespace lucia.Tests.Auth;

/// <summary>Real HTTPS/SQLite owner/member boundary and the production HA REST/registry adapter, synthetic read-only source only.</summary>
public sealed class CompanionLayoutWireTests
{
    [Fact]
    public async Task OwnerSaveAndMemberReadUseFreshRegistryWireAndNeverSubstituteStaleMetadataAfterFailureOrRevocation()
    {
        await using var source = await SyntheticCompanionHomeSource.StartAsync();
        await using var host = await CompanionSecurityTestHost.StartAsync(homeSource: source.Client, sourceBaseUrl: source.Origin);
        var layout = new CompanionLayout("home-a", source.Origin, "Our home",
            [new("front", "Front step", "Door", "porch"), new("sleep", "PRIVATE_BEDROOM", "Bed", "bedroom")], [new("front", "sleep")],
            [new("front", "porch", "sensor.porch_temperature", CompanionField.Temperature), new("sleep", "bedroom", "sensor.bedroom_temperature", CompanionField.Temperature)]);
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        using var saved = await CompanionLayoutApiTests.PutAsync(host, layout, null); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        host.Client.DefaultRequestHeaders.Remove("X-API-Key");
        var issued = await host.Sessions.CreateAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])]), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", issued.AccessToken);
        using var response = await host.Client.GetAsync("/api/companion/homes/home-a/layout"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        foreach (var hidden in new[] { "PRIVATE", "bedroom", "private-wire", source.Origin, "owner-a", "21.5", "readings", "numberValue" }) { Assert.DoesNotContain(hidden, body); }
        var projected = JsonDocument.Parse(body).RootElement;
        Assert.Single(projected.GetProperty("places").EnumerateArray()); Assert.Single(projected.GetProperty("bindings").EnumerateArray());
        Assert.Empty(projected.GetProperty("connections").EnumerateArray()); Assert.False(projected.GetProperty("hasUnqualifiedBindings").GetBoolean());
        Assert.Equal(new[] { "config/area_registry/list", "config/device_registry/list", "config/entity_registry/list", "states" }, source.Reads.Distinct().Order(StringComparer.Ordinal));
        source.ReportsUnavailable = true;
        using var unavailable = await host.Client.GetAsync("/api/companion/homes/home-a/layout"); Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.DoesNotContain("Front step", await unavailable.Content.ReadAsStringAsync());
        source.ReportsUnavailable = false;
        var restored = await host.Client.GetFromJsonAsync<JsonElement>("/api/companion/homes/home-a/layout");
        Assert.Equal(projected.GetProperty("revision").GetString(), restored.GetProperty("revision").GetString());
        await host.Sessions.RevokeAsync(issued.SessionId, "home-a");
        using var revoked = await host.Client.GetAsync("/api/companion/homes/home-a/layout"); Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        Assert.DoesNotContain("Front step", await revoked.Content.ReadAsStringAsync());
    }
}

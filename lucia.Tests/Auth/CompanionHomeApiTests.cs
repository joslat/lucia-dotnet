using System.Net.Http.Json;
using System.Text.Json;
using FakeItEasy;
using lucia.Agents.Companion;
using lucia.HomeAssistant.Models;

namespace lucia.Tests.Auth;

public sealed class CompanionHomeApiTests
{
    [Fact]
    public async Task SnapshotFiltersEveryPlaceSourceFieldAndAttributeBeforeSerialization()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        ConfigureHome(host);
        var tokens = await host.Sessions.CreateAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])]), "Synthetic phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        var response = await host.Client.GetAsync("/api/companion/homes/home-a/snapshot");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private-bedroom-marker", body);
        Assert.DoesNotContain("excluded-humidity-marker", body);
        Assert.DoesNotContain("private-context-marker", body);
        Assert.DoesNotContain("private-attribute-marker", body);
        Assert.DoesNotContain(tokens.AccessToken, body);
        var data = JsonDocument.Parse(body).RootElement;
        Assert.Equal("home-a", data.GetProperty("homeId").GetString());
        var place = Assert.Single(data.GetProperty("places").EnumerateArray());
        Assert.Equal("porch", place.GetProperty("placeId").GetString());
        var source = Assert.Single(place.GetProperty("sources").EnumerateArray());
        Assert.Equal("sensor.porch_temperature", source.GetProperty("sourceId").GetString());
        Assert.Equal("Temperature", source.GetProperty("field").GetString());
        var reading = Assert.Single(place.GetProperty("readings").EnumerateArray());
        Assert.Equal(21.5, reading.GetProperty("numberValue").GetDouble());
        Assert.Equal("Current", reading.GetProperty("quality").GetString());
    }

    [Fact]
    public async Task DiscoveryContainsOnlyAuthorizedMetadataWithoutValuesAndRejectsCrossHome()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        ConfigureHome(host);
        var tokens = await host.Sessions.CreateAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])]), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        var response = await host.Client.GetAsync("/api/companion/homes/home-a/discovery");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("numberValue", body);
        Assert.DoesNotContain("readings", body);
        Assert.DoesNotContain("private-bedroom-marker", body);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/companion/homes/home-b/snapshot")).StatusCode);
        host.Client.DefaultRequestHeaders.Authorization = null;
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/companion/homes/home-a/snapshot")).StatusCode);
    }

    [Fact]
    public void SourceLastReportedRoundTripsIndependentlyFromLastChanged()
    {
        const string json = """{"entity_id":"binary_sensor.porch","state":"off","last_changed":"2026-10-20T12:00:00Z","last_updated":"2026-10-20T12:00:00Z","last_reported":"2026-10-24T12:00:00Z"}""";
        var state = JsonSerializer.Deserialize<HomeAssistantState>(json)!;
        Assert.Contains("\"last_reported\":\"2026-10-24T12:00:00", JsonSerializer.Serialize(state));
        Assert.Equal(new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc), state.LastChanged);
    }

    [Fact]
    public async Task OwnerDiscoveryRequiresOwnerAuthorityAndNeverReturnsObservedValues()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        ConfigureHome(host);
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        var response = await host.Client.GetAsync("/api/companion/owner/discovery");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("private-bedroom-marker", body); // This is explicitly an owner catalog, not a member read.
        Assert.Contains("excluded-humidity-marker", body);
        Assert.DoesNotContain("numberValue", body);
        Assert.DoesNotContain("private-attribute-marker", body);
        host.Client.DefaultRequestHeaders.Remove("X-API-Key");
        var tokens = await host.Sessions.CreateAsync(new("home-a", "member-a", [], canReadEngine: true), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        host.Client.DefaultRequestHeaders.Add("Cookie", ".lucia.session=synthetic-cookie");
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/companion/owner/discovery")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevocationDuringSourceReadDiscardsTheEntireResponse(bool sourceFails)
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        ConfigureHome(host);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<HomeAssistantState[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => host.HomeAssistant.GetStatesAsync(A<CancellationToken>._)).ReturnsLazily(() => { entered.SetResult(); return release.Task; });
        var tokens = await host.Sessions.CreateAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])]), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        try
        {
            var pending = host.Client.GetAsync("/api/companion/homes/home-a/snapshot");
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await host.Sessions.RevokeAsync(tokens.SessionId, "home-a");
            if (sourceFails) { release.SetException(new HttpRequestException("private-upstream-marker")); }
            else { release.SetResult([State("sensor.porch_temperature", "21.5", "temperature", "°C")]); }
            var response = await pending;
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.DoesNotContain("21.5", await response.Content.ReadAsStringAsync());
        }
        finally { release.TrySetResult([]); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnerRevocationDuringDiscoveryDiscardsBothCatalogAndSourceFailure(bool sourceFails)
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        ConfigureHome(host);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<HomeAssistantState[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => host.HomeAssistant.GetStatesAsync(A<CancellationToken>._)).ReturnsLazily(() => { entered.SetResult(); return release.Task; });
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        try
        {
            var pending = host.Client.GetAsync("/api/companion/owner/discovery");
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            A.CallTo(() => host.Keys.GetKeyAsync("owner-a", A<CancellationToken>._)).Returns((lucia.Agents.Auth.ApiKeySummary?)null);
            if (sourceFails) { release.SetException(new HttpRequestException("private-upstream-marker")); }
            else { release.SetResult([State("sensor.porch_temperature", "21.5", "temperature", "°C")]); }
            var response = await pending;
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.DoesNotContain("private-", await response.Content.ReadAsStringAsync());
        }
        finally { release.TrySetResult([]); }
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("discovery")]
    public async Task OversizedProjectionIsRejectedWithoutPartialHouseholdData(string route)
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        ConfigureHome(host);
        var ids = Enumerable.Range(0, 2000).Select(index => "sensor.long_temperature_source_identifier_" + index).ToArray();
        A.CallTo(() => host.HomeAssistant.GetEntityRegistryAsync(A<CancellationToken>._)).Returns(ids.Select(id => new EntityRegistryEntry
        { EntityId = id, AreaId = "porch", Name = new string('a', 200) }).ToArray());
        A.CallTo(() => host.HomeAssistant.GetStatesAsync(A<CancellationToken>._)).Returns(ids.Select(id => State(id, "21.5", "temperature", "°C")).ToArray());
        var tokens = await host.Sessions.CreateAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])]), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        var response = await host.Client.GetAsync("/api/companion/homes/home-a/" + route);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("home_projection_too_large", body);
        Assert.DoesNotContain("long_temperature_source", body);
        Assert.DoesNotContain("Porch", body);
    }

    [Fact]
    public async Task UpstreamFailureAndSourceHomeChangeNeverBecomeAnEmptyOrCachedHome()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        ConfigureHome(host);
        var tokens = await host.Sessions.CreateAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])]), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        A.CallTo(() => host.HomeAssistant.GetStatesAsync(A<CancellationToken>._)).ThrowsAsync(new HttpRequestException("private-upstream-body-token"));
        var failed = await host.Client.GetAsync("/api/companion/homes/home-a/snapshot");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.DoesNotContain("private-upstream-body-token", await failed.Content.ReadAsStringAsync());
        A.CallTo(() => host.HomeAssistant.GetStatesAsync(A<CancellationToken>._)).Invokes(() => host.SourceSettings.BaseUrl = "http://another-home.invalid")
            .Returns([State("sensor.porch_temperature", "21.5", "temperature", "°C")]);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await host.Client.GetAsync("/api/companion/homes/home-a/snapshot")).StatusCode);
    }

    [Fact]
    public async Task AnEmptyFieldGrantExposesNoPlaceNamesAndDoesNotFetchUpstream()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        ConfigureHome(host);
        var tokens = await host.Sessions.CreateAsync(new("home-a", "member-a", [new("porch", [])]), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        var response = await host.Client.GetFromJsonAsync<JsonElement>("/api/companion/homes/home-a/snapshot");
        Assert.Empty(response.GetProperty("places").EnumerateArray());
        A.CallTo(() => host.HomeAssistant.GetStatesAsync(A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => host.HomeAssistant.GetAreaRegistryAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    internal static void ConfigureHome(CompanionSecurityTestHost host) => ConfigureHome(host.HomeAssistant);

    internal static void ConfigureHome(lucia.HomeAssistant.Services.IHomeAssistantClient source)
    {
        A.CallTo(() => source.GetAreaRegistryAsync(A<CancellationToken>._)).Returns([
            new() { AreaId = "porch", Name = "Porch" }, new() { AreaId = "bedroom", Name = "private-bedroom-marker" }
        ]);
        A.CallTo(() => source.GetEntityRegistryAsync(A<CancellationToken>._)).Returns([
            new() { EntityId = "sensor.porch_temperature", AreaId = "porch", OriginalName = "Porch temperature" },
            new() { EntityId = "sensor.porch_humidity", AreaId = "porch", OriginalName = "excluded-humidity-marker" },
            new() { EntityId = "sensor.bedroom_temperature", AreaId = "bedroom", OriginalName = "private-bedroom-marker" }
        ]);
        A.CallTo(() => source.GetStatesAsync(A<CancellationToken>._)).Returns([
            State("sensor.porch_temperature", "21.5", "temperature", "°C"),
            State("sensor.porch_humidity", "89", "humidity", "%"),
            State("sensor.bedroom_temperature", "19", "temperature", "°C")
        ]);
    }
    internal static HomeAssistantState State(string id, string value, string deviceClass, string? unit = null) => new()
    {
        EntityId = id, State = value, LastUpdated = DateTime.UtcNow,
        Attributes = new() { ["device_class"] = deviceClass, ["unit_of_measurement"] = unit ?? string.Empty, ["private-attribute-marker"] = "private-attribute-marker" },
        Context = new() { UserId = "private-context-marker" }
    };
}

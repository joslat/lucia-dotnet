using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FakeItEasy;
using lucia.Agents.Companion;

namespace lucia.Tests.Auth;

public sealed class CompanionLayoutApiTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter<CompanionField>(allowIntegerValues: false) } };

    [Fact]
    public async Task OwnerCreatesAndReplacesCheckedMetadataButStaleSaveCannotOverwrite()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync(); CompanionHomeApiTests.ConfigureHome(host);
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/api/companion/owner/layout")).StatusCode);
        using var create = await PutAsync(host, Layout(), null); Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        Assert.True(create.Headers.CacheControl?.NoStore);
        var first = await create.Content.ReadFromJsonAsync<JsonElement>(); var revision = first.GetProperty("revision").GetString()!;
        using var read = await host.Client.GetAsync("/api/companion/owner/layout"); Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.DoesNotContain("numberValue", await read.Content.ReadAsStringAsync());
        Assert.Equal(revision, (await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await PutAsync(host, Layout("Unreviewed create"), null)).StatusCode);
        using var replace = await PutAsync(host, Layout("Changed"), revision); Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PutAsync(host, Layout("Stale"), revision)).StatusCode);
        Assert.Equal("Changed", (await host.Layouts.ReadAsync("home-a"))!.Layout.Name);
    }

    [Fact]
    public async Task MemberGetsOnlyCurrentAllowedTopologyAndItsRevisionIgnoresHiddenChanges()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync(); CompanionHomeApiTests.ConfigureHome(host);
        var stored = (await host.Layouts.TrySaveAsync(Layout(), null, "owner-a"))!;
        var tokens = await host.Sessions.CreateAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])]), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        using var response = await host.Client.GetAsync("/api/companion/homes/home-a/layout"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        foreach (var hidden in new[] { "PRIVATE_", "bedroom", "sensor.porch_humidity", "synthetic-ha.invalid", "owner-a", stored.Revision, "updatedBy", "updatedAt", "readings", "numberValue" }) { Assert.DoesNotContain(hidden, text); }
        var data = JsonDocument.Parse(text).RootElement;
        Assert.Single(data.GetProperty("places").EnumerateArray()); Assert.Empty(data.GetProperty("connections").EnumerateArray()); Assert.Single(data.GetProperty("bindings").EnumerateArray());
        Assert.Equal("member-a", data.GetProperty("memberId").GetString());
        var revision = data.GetProperty("revision").GetString();
        var original = Layout(); var hiddenChange = new CompanionLayout(original.HomeId, original.SourceBaseUrl, original.Name,
            [original.Places[0], original.Places[1] with { Name = "PRIVATE_DIFFERENT_NAME" }], [], original.Bindings.Where(binding => binding.Field != CompanionField.Humidity).ToArray());
        Assert.NotNull(await host.Layouts.TrySaveAsync(hiddenChange, stored.Revision, "owner-b"));
        var second = await host.Client.GetFromJsonAsync<JsonElement>("/api/companion/homes/home-a/layout");
        Assert.Equal(revision, second.GetProperty("revision").GetString());
        Assert.False(second.GetProperty("hasUnqualifiedBindings").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/companion/homes/home-b/layout")).StatusCode);
        host.Client.DefaultRequestHeaders.Add("Cookie", ".lucia.session=synthetic-cookie");
        Assert.Equal(HttpStatusCode.Forbidden, (await PutAsync(host, Layout(), null)).StatusCode);
    }

    [Fact]
    public async Task CookieWritesNeedAntiforgeryAndNativeCredentialsCannotBorrowOwnerCookie()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync(); CompanionHomeApiTests.ConfigureHome(host);
        host.Client.DefaultRequestHeaders.Add("Cookie", ".lucia.session=synthetic-cookie");
        Assert.Equal(HttpStatusCode.Forbidden, (await PutAsync(host, Layout(), null)).StatusCode);
        using var proof = await host.Client.GetAsync("/api/companion/owner/antiforgery");
        var csrf = await proof.Content.ReadFromJsonAsync<JsonElement>(); var cookie = Assert.Single(proof.Headers.GetValues("Set-Cookie")).Split(';')[0];
        host.Client.DefaultRequestHeaders.Remove("Cookie"); host.Client.DefaultRequestHeaders.Add("Cookie", ".lucia.session=synthetic-cookie; " + cookie);
        host.Client.DefaultRequestHeaders.Add(csrf.GetProperty("headerName").GetString()!, csrf.GetProperty("requestToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(host, Layout(), null)).StatusCode);
        var tokens = await host.Sessions.CreateAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])]), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/companion/owner/layout")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PutAsync(host, Layout(), null)).StatusCode);
    }

    [Theory]
    [InlineData("missing-area")]
    [InlineData("moved-source")]
    [InlineData("foreign-origin")]
    [InlineData("foreign-home")]
    public async Task OwnerSaveMustMatchTheFreshCanonicalSource(string defect)
    {
        await using var host = await CompanionSecurityTestHost.StartAsync(); CompanionHomeApiTests.ConfigureHome(host);
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        var original = Layout();
        var layout = defect switch
        {
            "foreign-origin" => new CompanionLayout("home-a", "http://other.invalid", original.Name, original.Places, original.Connections, original.Bindings),
            "foreign-home" => new CompanionLayout("home-b", original.SourceBaseUrl, original.Name, original.Places, original.Connections, original.Bindings),
            _ => original
        };
        if (defect == "missing-area") { A.CallTo(() => host.HomeAssistant.GetAreaRegistryAsync(A<CancellationToken>._)).Returns([]); }
        if (defect == "moved-source") { A.CallTo(() => host.HomeAssistant.GetEntityRegistryAsync(A<CancellationToken>._)).Returns([new() { EntityId = "sensor.porch_temperature", AreaId = "bedroom" }]); }
        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync(host, layout, null)).StatusCode);
        Assert.Null(await host.Layouts.ReadAsync("home-a"));
    }

    [Fact]
    public async Task NoEligibleLayoutIsNotAnOracleForGlobalConfigurationOrSourceAvailability()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        Assert.NotNull(await host.Layouts.TrySaveAsync(Layout(), null, "owner-a"));
        var tokens = await host.Sessions.CreateAsync(new("home-a", "member-a", [new("porch", [])]), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/api/companion/homes/home-a/layout")).StatusCode);
        A.CallTo(() => host.HomeAssistant.GetStatesAsync(A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => host.HomeAssistant.GetAreaRegistryAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    internal static CompanionLayout Layout(string name = "Our home") => new("home-a", "http://synthetic-ha.invalid", name,
        [new("front", "Front step", "Door", "porch"), new("sleep", "PRIVATE_BEDROOM", "Bed", "bedroom")],
        [new("front", "sleep")], [new("front", "porch", "sensor.porch_temperature", CompanionField.Temperature),
            new("front", "porch", "sensor.porch_humidity", CompanionField.Humidity), new("sleep", "bedroom", "sensor.bedroom_temperature", CompanionField.Temperature)]);
    internal static Task<HttpResponseMessage> PutAsync(CompanionSecurityTestHost host, CompanionLayout layout, string? revision) =>
        host.Client.PutAsJsonAsync("/api/companion/owner/layout", new { expectedRevision = revision, layout }, Json);

    [Fact]
    public async Task APreviouslyAllowedBindingThatMovesAwayIsOmittedAndRequiresReviewWithoutNamingItsNewRoom()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync(); CompanionHomeApiTests.ConfigureHome(host);
        Assert.NotNull(await host.Layouts.TrySaveAsync(Layout(), null, "owner-a"));
        var issued = await host.Sessions.CreateAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])]), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", issued.AccessToken);
        var before = await host.Client.GetFromJsonAsync<JsonElement>("/api/companion/homes/home-a/layout");
        A.CallTo(() => host.HomeAssistant.GetEntityRegistryAsync(A<CancellationToken>._)).Returns([
            new() { EntityId = "sensor.porch_temperature", AreaId = "bedroom" }, new() { EntityId = "sensor.porch_humidity", AreaId = "porch" }]);
        using var response = await host.Client.GetAsync("/api/companion/homes/home-a/layout"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("bedroom", body); Assert.DoesNotContain("sensor.porch_temperature", body);
        var after = JsonDocument.Parse(body).RootElement; Assert.Empty(after.GetProperty("bindings").EnumerateArray());
        Assert.True(after.GetProperty("hasUnqualifiedBindings").GetBoolean());
        Assert.NotEqual(before.GetProperty("revision").GetString(), after.GetProperty("revision").GetString());
    }

    [Fact]
    public async Task OwnerReadReportsAChangedLiveSourceBindingWithoutHidingTheDocumentNeededForRecovery()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        Assert.NotNull(await host.Layouts.TrySaveAsync(Layout(), null, "owner-a"));
        host.SourceSettings.BaseUrl = "http://another-home.invalid";
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        using var response = await host.Client.GetAsync("/api/companion/owner/layout"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var view = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(view.GetProperty("sourceMatches").GetBoolean());
        Assert.Equal("Our home", view.GetProperty("layout").GetProperty("name").GetString());
        A.CallTo(() => host.HomeAssistant.GetStatesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task FirstCreateContextIsAvailableOnlyToTheCurrentOwnerNotToAMemberMissingView()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        using var owner = await host.Client.GetAsync("/api/companion/owner/layout"); Assert.Equal(HttpStatusCode.NotFound, owner.StatusCode);
        var context = await owner.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("home-a", context.GetProperty("homeId").GetString());
        Assert.Equal("http://synthetic-ha.invalid", context.GetProperty("sourceBaseUrl").GetString());
        Assert.True(context.GetProperty("sourceMatches").GetBoolean());
        A.CallTo(() => host.HomeAssistant.GetStatesAsync(A<CancellationToken>._)).MustNotHaveHappened();
        host.Client.DefaultRequestHeaders.Remove("X-API-Key");
        var issued = await host.Sessions.CreateAsync(new("home-a", "member-a", [new("porch", [CompanionField.Temperature])]), "Phone");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", issued.AccessToken);
        using var member = await host.Client.GetAsync("/api/companion/homes/home-a/layout"); Assert.Equal(HttpStatusCode.NotFound, member.StatusCode);
        var body = await member.Content.ReadAsStringAsync(); Assert.DoesNotContain("sourceBaseUrl", body); Assert.DoesNotContain("synthetic-ha", body);
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using Lucia.Companion.Connection;
using lucia.Agents.Companion;

namespace lucia.Tests.Auth;

/// <summary>Actual native transport and protected leases over HTTPS/SQLite and synthetic HA protocol, not installed-device proof.</summary>
public sealed class CompanionSharedLayoutNativeWireTests
{
    [Fact]
    public async Task IndependentProtectedClientsReadOwnerUpdatesRecoverFromSourceFailureAndClearRevokedCredentials()
    {
        await using var source = await SyntheticCompanionHomeSource.StartAsync();
        await using var host = await CompanionSecurityTestHost.StartAsync(homeSource: source.Client, sourceBaseUrl: source.Origin);
        var directory = Directory.CreateTempSubdirectory("lucia-layout-native-wire-");
        try
        {
            var phoneVault = new ProtectedSessionVault(Path.Combine(directory.FullName, "phone"), CompanionCoordinatorIntegrationTests.EphemeralProtector());
            var tabletVault = new ProtectedSessionVault(Path.Combine(directory.FullName, "tablet"), CompanionCoordinatorIntegrationTests.EphemeralProtector());
            var grant = new CompanionGrant("home-a", "member-a", [new("porch", [CompanionField.Temperature])]);
            var phoneTokens = await host.Sessions.CreateAsync(grant, "Phone");
            var tabletTokens = await host.Sessions.CreateAsync(grant, "Tablet");
            using var seed = host.CreateNativeClient();
            await new DeviceSessionCoordinator(seed, phoneVault).ConnectAsync(new(phoneTokens.SessionId, phoneTokens.AccessToken,
                phoneTokens.AccessExpiresAt, phoneTokens.RefreshToken, phoneTokens.RefreshExpiresAt));
            await new DeviceSessionCoordinator(seed, tabletVault).ConnectAsync(new(tabletTokens.SessionId, tabletTokens.AccessToken,
                tabletTokens.AccessExpiresAt, tabletTokens.RefreshToken, tabletTokens.RefreshExpiresAt));
            using var phone = new CompanionConnection(phoneVault, _ => host.CreateNativeClient());
            using var tablet = new CompanionConnection(tabletVault, _ => host.CreateNativeClient());
            var phoneIdentity = (await phone.RestoreAsync())!; var tabletIdentity = (await tablet.RestoreAsync())!;
            Assert.Null(await phone.ReadSharedLayoutAsync(phoneIdentity));
            Assert.True(phone.HasSavedConnection);
            host.Client.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
            var ownerRevision = await SaveAsync(host, Layout(source.Origin), null);
            var phoneFirst = Assert.IsType<SharedHomeLayout>(await phone.ReadSharedLayoutAsync(phoneIdentity));
            var tabletFirst = Assert.IsType<SharedHomeLayout>(await tablet.ReadSharedLayoutAsync(tabletIdentity));
            Assert.Equal("Front step", Assert.Single(phoneFirst.Places).Name);
            Assert.Equal(phoneFirst.Places, tabletFirst.Places); Assert.NotEqual(phoneFirst.Revision, tabletFirst.Revision);
            Assert.Single(phoneFirst.Bindings); Assert.Empty(phoneFirst.Connections);
            foreach (var hidden in new[] { "PRIVATE", "bedroom", source.Origin, ownerRevision, "UpdatedBy", "Readings", "NumberValue" })
            { Assert.DoesNotContain(hidden, JsonSerializer.Serialize(phoneFirst)); }
            ownerRevision = await SaveAsync(host, Layout(source.Origin, hiddenName: "PRIVATE_CHANGED"), ownerRevision);
            Assert.Equal(phoneFirst.Revision, (await phone.ReadSharedLayoutAsync(phoneIdentity))!.Revision);
            Assert.Equal(tabletFirst.Revision, (await tablet.ReadSharedLayoutAsync(tabletIdentity))!.Revision);
            await SaveAsync(host, Layout(source.Origin, visibleName: "Front porch"), ownerRevision);
            var phoneUpdated = (await phone.ReadSharedLayoutAsync(phoneIdentity))!;
            var tabletUpdated = (await tablet.ReadSharedLayoutAsync(tabletIdentity))!;
            Assert.Equal("Front porch", Assert.Single(phoneUpdated.Places).Name);
            Assert.Equal(phoneUpdated.Places, tabletUpdated.Places);
            Assert.NotEqual(phoneFirst.Revision, phoneUpdated.Revision); Assert.NotEqual(tabletFirst.Revision, tabletUpdated.Revision);
            source.ReportsUnavailable = true;
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Assert.ThrowsAsync<CompanionConnectionException>(() => phone.ReadSharedLayoutAsync(phoneIdentity))).StatusCode);
            Assert.True(phone.HasSavedConnection);
            source.ReportsUnavailable = false;
            Assert.Equal(phoneUpdated.Revision, (await phone.ReadSharedLayoutAsync(phoneIdentity))!.Revision);
            await host.Sessions.RevokeAsync(phoneTokens.SessionId, "home-a");
            Assert.Equal(ConnectionFailure.Unauthorized, (await Assert.ThrowsAsync<CompanionConnectionException>(() => phone.ReadSharedLayoutAsync(phoneIdentity))).Failure);
            Assert.False(phone.HasSavedConnection);
            await using (var lease = await phoneVault.OpenAsync()) { Assert.Null(await lease.ReadAsync()); }
            Assert.Equal(tabletUpdated.Revision, (await tablet.ReadSharedLayoutAsync(tabletIdentity))!.Revision);
            Assert.True(tablet.HasSavedConnection);
            Assert.Equal(new[] { "config/area_registry/list", "config/device_registry/list", "config/entity_registry/list", "states" }, source.Reads.Distinct().Order(StringComparer.Ordinal));
        }
        finally { directory.Delete(recursive: true); }
    }

    private static CompanionLayout Layout(string origin, string visibleName = "Front step", string hiddenName = "PRIVATE_BEDROOM") => new("home-a", origin, "Our home",
        [new("front", visibleName, "Door", "porch"), new("sleep", hiddenName, "Bed", "bedroom")], [new("front", "sleep")],
        [new("front", "porch", "sensor.porch_temperature", CompanionField.Temperature), new("sleep", "bedroom", "sensor.bedroom_temperature", CompanionField.Temperature)]);

    private static async Task<string> SaveAsync(CompanionSecurityTestHost host, CompanionLayout layout, string? revision)
    {
        using var response = await CompanionLayoutApiTests.PutAsync(host, layout, revision);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetString()!;
    }
}

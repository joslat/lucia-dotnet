using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lucia.Companion.Connection;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class CompanionActivityCoordinatorTests
{
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    [Fact]
    public async Task RemovingActivityAuthorityStopsTheViewBeforeADataRequestWithoutDeletingAValidHomeConnection()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-activity-access-");
        using var protector = new TestSecretProtector(); var vault = new ProtectedSessionVault(directory.FullName, protector);
        try
        {
            var credentials = new DeviceCredentials(new string('a', 32), "lca_" + new string('a', 43), DateTimeOffset.UtcNow.AddMinutes(5),
                "lcr_" + new string('a', 43), DateTimeOffset.UtcNow.AddDays(7));
            await using (var lease = await vault.OpenAsync(TestContext.Current.CancellationToken))
            { await lease.SaveAsync(new(1, new Uri("https://localhost"), credentials, SessionPhase.Active, "home-a", "member-a"), TestContext.Current.CancellationToken); }
            var reads = 0;
            using var api = new CompanionApiClient(new Uri("https://localhost"), new ScriptedCompanionHandler((request, _) =>
            {
                Assert.EndsWith("/session", request.RequestUri!.AbsolutePath); reads++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new DeviceSession(new string('a', 32), "Phone",
                    DateTimeOffset.UtcNow.AddMinutes(5), "home-a", "member-a", [])) });
            }));
            var expected = new ConnectionSession(new Uri("https://localhost"), CompanionActivityClientTests.Session());
            await Assert.ThrowsAsync<ActivityAccessException>(() => new DeviceSessionCoordinator(api, vault).ReadActivityAsync(expected, TestContext.Current.CancellationToken));
            Assert.Equal(1, reads);
            await using var retained = await vault.OpenAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(await retained.ReadAsync(TestContext.Current.CancellationToken));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData("narrowed")]
    [InlineData("route-denied")]
    [InlineData("revoked")]
    [InlineData("changed-member")]
    [InlineData("changed-home-grant")]
    [InlineData("unavailable")]
    public async Task ALateActivityDenialRechecksTheSessionBeforeChoosingActivityOnlyOrConnectionRecovery(string outcome)
    {
        var directory = Directory.CreateTempSubdirectory("lucia-activity-late-access-");
        using var protector = new TestSecretProtector(); var vault = new ProtectedSessionVault(directory.FullName, protector);
        try
        {
            var credentials = new DeviceCredentials(new string('a', 32), "lca_" + new string('a', 43), DateTimeOffset.UtcNow.AddMinutes(5),
                "lcr_" + new string('a', 43), DateTimeOffset.UtcNow.AddDays(7));
            await using (var lease = await vault.OpenAsync(TestContext.Current.CancellationToken))
            { await lease.SaveAsync(new(1, new Uri("https://localhost"), credentials, SessionPhase.Active, "home-a", "member-a"), TestContext.Current.CancellationToken); }
            var protectedBefore = await File.ReadAllBytesAsync(Path.Combine(directory.FullName, "session.bin"), TestContext.Current.CancellationToken);
            var metadataReads = 0; var activityReads = 0; var homeReads = 0;
            using var service = new CompanionConnection(vault, endpoint => new(endpoint, new ScriptedCompanionHandler((request, _) =>
            {
                switch (request.RequestUri!.AbsolutePath)
                {
                    case "/api/companion/session":
                        metadataReads++;
                        if (metadataReads > 1 && outcome == "revoked") { return Reply(HttpStatusCode.Unauthorized); }
                        if (metadataReads > 1 && outcome == "unavailable") { return Reply(HttpStatusCode.ServiceUnavailable); }
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new DeviceSession(new string('a', 32), "Phone",
                            DateTimeOffset.UtcNow.AddMinutes(5), "home-a", metadataReads > 1 && outcome == "changed-member" ? "member-b" : "member-a",
                            metadataReads > 1 && outcome == "changed-home-grant" ? [new DevicePlaceGrant("kitchen", [Lucia.Companion.Core.ObservationRole.Temperature])] : [],
                            canReadActivity: !(metadataReads > 1 && outcome == "narrowed")), options: WireJson) });
                    case "/api/companion/homes/home-a/activity": activityReads++; return Reply(HttpStatusCode.Forbidden);
                    case "/api/companion/homes/home-a/snapshot": homeReads++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        { Content = JsonContent.Create(new HomeSnapshot("home-a", DateTimeOffset.UtcNow, 1800, [])) });
                    case "/api/companion/session/revoke": return Reply(HttpStatusCode.NoContent);
                    default: throw new InvalidOperationException("Unexpected synthetic route: " + request.RequestUri.AbsolutePath);
                }
            })));
            var expected = new ConnectionSession(new Uri("https://localhost"), CompanionActivityClientTests.Session());
            if (outcome is "narrowed" or "route-denied")
            {
                await Assert.ThrowsAsync<ActivityAccessException>(() => service.ReadActivityAsync(expected, TestContext.Current.CancellationToken));
                Assert.True(service.HasSavedConnection);
                Assert.Equal(protectedBefore, await File.ReadAllBytesAsync(Path.Combine(directory.FullName, "session.bin"), TestContext.Current.CancellationToken));
                Assert.Equal("home-a", (await service.ReadHomeAsync(expected, TestContext.Current.CancellationToken)).HomeId);
                Assert.Equal(1, homeReads);
            }
            else if (outcome is "changed-member" or "changed-home-grant")
            {
                Assert.Equal(SessionStateFailure.IdentityChanged, (await Assert.ThrowsAsync<SessionStateException>(() =>
                    service.ReadActivityAsync(expected, TestContext.Current.CancellationToken))).Failure);
            }
            else
            {
                var failure = await Assert.ThrowsAsync<CompanionConnectionException>(() => service.ReadActivityAsync(expected, TestContext.Current.CancellationToken));
                Assert.Equal(outcome == "revoked" ? ConnectionFailure.Unauthorized : ConnectionFailure.Rejected, failure.Failure);
                Assert.Equal(outcome == "unavailable", service.HasSavedConnection);
            }
            Assert.Equal(1, activityReads); Assert.Equal(outcome is "narrowed" or "route-denied" ? 3 : 2, metadataReads);
            await using var retained = await vault.OpenAsync(TestContext.Current.CancellationToken);
            if (outcome is "revoked" or "changed-member") { Assert.Null(await retained.ReadAsync(TestContext.Current.CancellationToken)); }
            else { Assert.NotNull(await retained.ReadAsync(TestContext.Current.CancellationToken)); }
        }
        finally { directory.Delete(recursive: true); }
    }

    private static Task<HttpResponseMessage> Reply(HttpStatusCode status) => Task.FromResult(new HttpResponseMessage(status));
}

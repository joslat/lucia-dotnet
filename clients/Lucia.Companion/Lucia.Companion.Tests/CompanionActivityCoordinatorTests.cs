using System.Net;
using System.Net.Http.Json;
using Lucia.Companion.Connection;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class CompanionActivityCoordinatorTests
{
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
            var error = await Assert.ThrowsAsync<SessionStateException>(() => new DeviceSessionCoordinator(api, vault).ReadActivityAsync(expected, TestContext.Current.CancellationToken));
            Assert.Equal(SessionStateFailure.IdentityChanged, error.Failure); Assert.Equal(1, reads);
            await using var retained = await vault.OpenAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(await retained.ReadAsync(TestContext.Current.CancellationToken));
        }
        finally { directory.Delete(recursive: true); }
    }
}

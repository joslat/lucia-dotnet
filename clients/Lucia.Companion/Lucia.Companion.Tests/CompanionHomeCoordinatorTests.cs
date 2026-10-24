using System.Net;
using System.Net.Http.Json;
using System.Text;
using Lucia.Companion.Connection;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class CompanionHomeCoordinatorTests
{
    [Fact]
    public async Task HomeReadRenewsAndProtectsReplacementThenVerifiesGrantBeforeReadingData()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-home-renew-");
        using var protector = new TestSecretProtector();
        var vault = new ProtectedSessionVault(directory.FullName, protector);
        var next = Credentials('c');
        var paths = new List<string>();
        try
        {
            await SeedAsync(vault, Credentials('a', expired: true));
            using var api = Api((request, _) =>
            {
                var path = request.RequestUri!.AbsolutePath;
                paths.Add(path);
                if (path.EndsWith("/renew", StringComparison.Ordinal)) { return Task.FromResult(Ok(next)); }
                var cleartext = protector.Unprotect(File.ReadAllBytes(Path.Combine(directory.FullName, "session.bin")));
                try
                {
                    var json = Encoding.UTF8.GetString(cleartext);
                    Assert.Contains(next.AccessToken, json);
                    Assert.DoesNotContain("readings", json);
                    Assert.DoesNotContain("places", json);
                }
                finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(cleartext); }
                Assert.Equal(next.AccessToken, request.Headers.Authorization!.Parameter);
                return Task.FromResult(path.EndsWith("/session", StringComparison.Ordinal) ? Ok(CompanionHomeClientTests.Grant()) : Data());
            });
            Assert.Single((await new DeviceSessionCoordinator(api, vault).ReadHomeAsync(TestContext.Current.CancellationToken)).Places);
            Assert.Equal(new[] { "/api/companion/session/renew", "/api/companion/session", "/api/companion/homes/home-a/snapshot" }, paths);
            await using var lease = await vault.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal(next.AccessToken, (await lease.ReadAsync(TestContext.Current.CancellationToken))!.Credentials.AccessToken);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, true)]
    [InlineData(HttpStatusCode.Forbidden, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    public async Task ReadFailureNeverBecomesDataAndOnlyLostAuthorityRemovesTheProtectedConnection(HttpStatusCode failure, bool cleared)
    {
        var directory = Directory.CreateTempSubdirectory("lucia-home-denied-");
        using var protector = new TestSecretProtector();
        var vault = new ProtectedSessionVault(directory.FullName, protector);
        try
        {
            await SeedAsync(vault, Credentials('a'));
            using var api = Api((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
                ? Ok(CompanionHomeClientTests.Grant()) : new HttpResponseMessage(failure) { Content = new StringContent("private-error-body") }));
            var error = await Assert.ThrowsAsync<CompanionConnectionException>(() => new DeviceSessionCoordinator(api, vault).ReadHomeAsync(TestContext.Current.CancellationToken));
            Assert.DoesNotContain("private-error-body", error.ToString());
            await using var lease = await vault.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal(cleared, await lease.ReadAsync(TestContext.Current.CancellationToken) is null);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task ExclusiveProtectedLeaseCoversTheEntireHomeReadAndMetadataOnlyDiscoveryAlsoWorks()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-home-lease-");
        using var protector = new TestSecretProtector();
        var vault = new ProtectedSessionVault(directory.FullName, protector);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await SeedAsync(vault, Credentials('a'));
            using var api = Api(async (request, token) =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)) { return Ok(CompanionHomeClientTests.Grant()); }
                if (request.RequestUri.AbsolutePath.EndsWith("/discovery", StringComparison.Ordinal))
                {
                    var catalog = CompanionHomeClientTests.Payload();
                    catalog.Remove("maximumObservationAgeSeconds");
                    catalog["places"]![0]!.AsObject().Remove("readings");
                    return Ok(catalog);
                }
                entered.SetResult();
                await release.Task.WaitAsync(token);
                return Data();
            });
            var coordinator = new DeviceSessionCoordinator(api, vault);
            var pending = coordinator.ReadHomeAsync(TestContext.Current.CancellationToken);
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
                wait.CancelAfter(TimeSpan.FromMilliseconds(50));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { await using var competing = await vault.OpenAsync(wait.Token); });
            }
            finally { release.TrySetResult(); }
            Assert.Single((await pending).Places);
            Assert.Single((await coordinator.DiscoverAsync(TestContext.Current.CancellationToken)).Places);
        }
        finally { release.TrySetResult(); directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task NoStoredConnectionMeansNoDataRequest()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-home-unpaired-");
        using var protector = new TestSecretProtector();
        try
        {
            using var api = Api((_, _) => throw new InvalidOperationException("No credential exists."));
            var coordinator = new DeviceSessionCoordinator(api, new ProtectedSessionVault(directory.FullName, protector));
            Assert.Equal(SessionStateFailure.ConnectionRequired, (await Assert.ThrowsAsync<SessionStateException>(() => coordinator.ReadHomeAsync(TestContext.Current.CancellationToken))).Failure);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task AViewBoundToAnotherSessionOrGrantCannotReadUnderAReplacementConnection()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-home-view-identity-");
        using var protector = new TestSecretProtector();
        var vault = new ProtectedSessionVault(directory.FullName, protector);
        var dataCalls = 0;
        try
        {
            await SeedAsync(vault, Credentials('a'));
            using var api = Api((request, _) =>
            {
                if (!request.RequestUri!.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)) { dataCalls++; }
                return Task.FromResult(Ok(CompanionHomeClientTests.Grant()));
            });
            var expected = new ConnectionSession(new Uri("https://localhost"), new DeviceSession(new string('b', 32), "Old phone", DateTimeOffset.UtcNow.AddDays(1), "home-a", "member-a", [new("porch", [Core.ObservationRole.Temperature])]));
            var coordinator = new DeviceSessionCoordinator(api, vault);
            Assert.Equal(SessionStateFailure.IdentityChanged, (await Assert.ThrowsAsync<SessionStateException>(() => coordinator.ReadHomeAsync(expected, TestContext.Current.CancellationToken))).Failure);
            var changedGrant = new DeviceSession(new string('a', 32), "Phone", DateTimeOffset.UtcNow.AddDays(1), "home-a", "member-a", []);
            await Assert.ThrowsAsync<SessionStateException>(() => coordinator.ReadHomeAsync(expected with { Session = changedGrant }, TestContext.Current.CancellationToken));
            Assert.Equal(0, dataCalls);
            await using var lease = await vault.OpenAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(await lease.ReadAsync(TestContext.Current.CancellationToken));
        }
        finally { directory.Delete(recursive: true); }
    }

    private static CompanionApiClient Api(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => new(new Uri("https://localhost"), new ScriptedCompanionHandler(send));
    private static HttpResponseMessage Ok(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body, options: new(System.Text.Json.JsonSerializerDefaults.Web)
    { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter<Core.ObservationRole>(allowIntegerValues: false) } }) };
    private static HttpResponseMessage Data() => Ok(CompanionHomeClientTests.Payload());
    private static DeviceCredentials Credentials(char marker, bool expired = false) => new(new string('a', 32), "lca_" + new string(marker, 43),
        DateTimeOffset.UtcNow.AddMinutes(expired ? -1 : 5), "lcr_" + new string(marker, 43), DateTimeOffset.UtcNow.AddDays(7));
    private static async Task SeedAsync(ISessionVault vault, DeviceCredentials credentials)
    {
        await using var lease = await vault.OpenAsync(TestContext.Current.CancellationToken);
        await lease.SaveAsync(new(1, new Uri("https://localhost"), credentials, SessionPhase.Active, "home-a", "member-a"), TestContext.Current.CancellationToken);
    }
}

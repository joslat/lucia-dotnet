using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lucia.Companion.Connection;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class CompanionSharedLayoutCoordinatorTests
{
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Theory]
    [InlineData("endpoint", false)]
    [InlineData("session", false)]
    [InlineData("home", true)]
    [InlineData("member", true)]
    [InlineData("fields", false)]
    [InlineData("place", false)]
    public async Task AChangedViewCannotReadSharedMetadataUnderReplacementAuthority(string difference, bool clear)
    {
        var directory = Directory.CreateTempSubdirectory("lucia-layout-identity-");
        using var protector = new TestSecretProtector(); var vault = new ProtectedSessionVault(directory.FullName, protector);
        try
        {
            await SeedAsync(vault);
            var expected = Expected();
            if (difference == "endpoint") { expected = expected with { Endpoint = new Uri("https://another.invalid") }; }
            if (difference == "session") { expected = expected with { Session = Session(sessionId: new string('b', 32)) }; }
            var calls = 0;
            using var api = Api((request, _) =>
            {
                calls++;
                if (request.RequestUri!.AbsolutePath.EndsWith("/revoke", StringComparison.Ordinal)) { return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)); }
                Assert.EndsWith("/session", request.RequestUri.AbsolutePath);
                return Task.FromResult(Ok(Session(home: difference == "home" ? "other-home" : "home-a",
                    member: difference == "member" ? "other-member" : "member-a",
                    places: difference == "fields" ? [new("porch", [])] : difference == "place" ? [new("other", [Core.ObservationRole.Contact])] : null)));
            });
            Assert.Equal(SessionStateFailure.IdentityChanged, (await Assert.ThrowsAsync<SessionStateException>(() =>
                new DeviceSessionCoordinator(api, vault).ReadSharedLayoutAsync(expected, TestContext.Current.CancellationToken))).Failure);
            Assert.Equal(clear ? 2 : 1, calls);
            await using var lease = await vault.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal(clear, await lease.ReadAsync(TestContext.Current.CancellationToken) is null);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, true)]
    [InlineData(HttpStatusCode.Forbidden, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    [InlineData(HttpStatusCode.TooManyRequests, false)]
    public async Task SharedReadDoesNotTurnFailuresIntoDataOrDeleteCredentialsForSourceFailures(HttpStatusCode failure, bool clear)
    {
        var directory = Directory.CreateTempSubdirectory("lucia-layout-failure-");
        using var protector = new TestSecretProtector(); var vault = new ProtectedSessionVault(directory.FullName, protector);
        try
        {
            await SeedAsync(vault);
            using var connection = new CompanionConnection(vault, endpoint => new(endpoint, new ScriptedCompanionHandler((request, _) =>
                Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/session", StringComparison.Ordinal) ? Ok(Session())
                    : new HttpResponseMessage(failure) { Content = new StringContent("PRIVATE_MARKER") }))));
            var error = await Assert.ThrowsAsync<CompanionConnectionException>(() => connection.ReadSharedLayoutAsync(Expected(), TestContext.Current.CancellationToken));
            Assert.Equal(failure, error.StatusCode); Assert.DoesNotContain("PRIVATE_MARKER", error.ToString());
            Assert.Equal(!clear, connection.HasSavedConnection);
            await using var lease = await vault.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal(clear, await lease.ReadAsync(TestContext.Current.CancellationToken) is null);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task RenewalIsDurableAndTheExclusiveLeaseCoversSharedReadWithoutPersistingMetadata()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-layout-renew-");
        using var protector = new TestSecretProtector(); var vault = new ProtectedSessionVault(directory.FullName, protector);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await SeedAsync(vault, expired: true);
            var next = Credentials('b'); var paths = new List<string>();
            using var connection = new CompanionConnection(vault, endpoint => new(endpoint, new ScriptedCompanionHandler(async (request, token) =>
            {
                var path = request.RequestUri!.AbsolutePath; paths.Add(path);
                if (path.EndsWith("/renew", StringComparison.Ordinal)) { return Ok(next); }
                Assert.Equal(next.AccessToken, request.Headers.Authorization!.Parameter);
                var bytes = protector.Unprotect(await File.ReadAllBytesAsync(Path.Combine(directory.FullName, "session.bin"), token));
                try
                {
                    var stored = System.Text.Encoding.UTF8.GetString(bytes);
                    Assert.Contains(next.AccessToken, stored); Assert.DoesNotContain("Our home", stored); Assert.DoesNotContain("binary_sensor.door", stored);
                }
                finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
                if (path.EndsWith("/session", StringComparison.Ordinal)) { return Ok(Session()); }
                Assert.EndsWith("/layout", path); entered.SetResult(); await release.Task.WaitAsync(token);
                return Ok(CompanionSharedLayoutClientTests.Payload());
            })));
            ICompanionConnection boundary = connection;
            var pending = boundary.ReadSharedLayoutAsync(Expected(), TestContext.Current.CancellationToken);
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
                wait.CancelAfter(TimeSpan.FromMilliseconds(50));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { await using var competing = await vault.OpenAsync(wait.Token); });
            }
            finally { release.TrySetResult(); }
            Assert.Equal("Our home", (await pending)!.Name);
            Assert.Equal(new[] { "/api/companion/session/renew", "/api/companion/session", "/api/companion/homes/home-a/layout" }, paths);
            await using var retained = await vault.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal(next.AccessToken, (await retained.ReadAsync(TestContext.Current.CancellationToken))!.Credentials.AccessToken);
        }
        finally { release.TrySetResult(); directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task NoSavedSessionMakesNoNetworkRequestAndMissingLayoutPreservesAValidConnection()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-layout-missing-");
        using var protector = new TestSecretProtector(); var vault = new ProtectedSessionVault(directory.FullName, protector);
        var calls = 0;
        try
        {
            using var connection = new CompanionConnection(vault, endpoint => new(endpoint, new ScriptedCompanionHandler((request, _) =>
            {
                calls++;
                return Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/session", StringComparison.Ordinal) ? Ok(Session())
                    : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = JsonContent.Create(new { error = "layout_not_configured" }) });
            })));
            Assert.Equal(SessionStateFailure.ConnectionRequired, (await Assert.ThrowsAsync<SessionStateException>(() =>
                connection.ReadSharedLayoutAsync(Expected(), TestContext.Current.CancellationToken))).Failure);
            Assert.Equal(0, calls);
            await SeedAsync(vault);
            Assert.Null(await connection.ReadSharedLayoutAsync(Expected(), TestContext.Current.CancellationToken));
            Assert.True(connection.HasSavedConnection); Assert.Equal(2, calls);
            await using var lease = await vault.OpenAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(await lease.ReadAsync(TestContext.Current.CancellationToken));
        }
        finally { directory.Delete(recursive: true); }
    }

    private static ConnectionSession Expected() => new(new Uri("https://localhost"), Session());
    private static DeviceSession Session(string? sessionId = null, string home = "home-a", string member = "member-a", IReadOnlyList<DevicePlaceGrant>? places = null) =>
        new(sessionId ?? new string('a', 32), "Phone", DateTimeOffset.UtcNow.AddDays(1), home, member, places ?? [new("porch", [Core.ObservationRole.Contact])]);
    private static DeviceCredentials Credentials(char marker, bool expired = false) => new(new string('a', 32), "lca_" + new string(marker, 43),
        DateTimeOffset.UtcNow.AddMinutes(expired ? -1 : 5), "lcr_" + new string(marker, 43), DateTimeOffset.UtcNow.AddDays(7));
    private static CompanionApiClient Api(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => new(new Uri("https://localhost"), new ScriptedCompanionHandler(send));
    private static HttpResponseMessage Ok(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body, options: WireJson) };
    private static async Task SeedAsync(ISessionVault vault, bool expired = false)
    {
        await using var lease = await vault.OpenAsync(TestContext.Current.CancellationToken);
        await lease.SaveAsync(new(1, new Uri("https://localhost"), Credentials('a', expired), SessionPhase.Active, "home-a", "member-a"), TestContext.Current.CancellationToken);
    }
}

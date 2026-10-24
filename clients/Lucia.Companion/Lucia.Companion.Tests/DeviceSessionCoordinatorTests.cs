using System.Net;
using System.Net.Http.Json;
using Lucia.Companion.Connection;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class DeviceSessionCoordinatorTests
{
    [Fact]
    public async Task SeparateCoordinatorsRenewOnceAndPersistBeforeUsingReplacement()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-coordinator-");
        using var protector = new TestSecretProtector();
        var vault = new ProtectedSessionVault(directory.FullName, protector);
        var next = Credentials(expired: false, marker: 'c');
        var renewals = 0;
        try
        {
            await SeedAsync(vault, SessionVaultTests.Record(SessionPhase.Active, Credentials(expired: true)));
            using var api = Api(async (request, token) =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/renew", StringComparison.Ordinal))
                {
                    Assert.Equal("Renewing", StoredPhase(directory.FullName, protector));
                    Interlocked.Increment(ref renewals);
                    await Task.Delay(30, token);
                    return Ok(next);
                }
                Assert.Equal("Active", StoredPhase(directory.FullName, protector));
                Assert.Equal(next.AccessToken, request.Headers.Authorization!.Parameter);
                return Metadata(next.SessionId);
            });
            var results = await Task.WhenAll(new DeviceSessionCoordinator(api, vault).RestoreAsync(TestContext.Current.CancellationToken),
                new DeviceSessionCoordinator(api, new ProtectedSessionVault(directory.FullName, protector)).RestoreAsync(TestContext.Current.CancellationToken));
            Assert.Equal(1, renewals);
            Assert.All(results, session => Assert.Equal("private-home", session!.HomeId));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task AmbiguousRenewalSurvivesRestartWithoutReplay()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-renewal-crash-");
        using var protector = new TestSecretProtector();
        var vault = new ProtectedSessionVault(directory.FullName, protector);
        var renewals = 0;
        var revocations = 0;
        try
        {
            await SeedAsync(vault, SessionVaultTests.Record(SessionPhase.Active, Credentials(expired: true)));
            using var api = Api((request, _) =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/renew", StringComparison.Ordinal))
                { renewals++; throw new HttpRequestException("Injected lost delivery."); }
                Assert.EndsWith("/revoke", request.RequestUri.AbsolutePath);
                revocations++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            });
            await Assert.ThrowsAsync<CompanionConnectionException>(() => new DeviceSessionCoordinator(api, vault).RestoreAsync(TestContext.Current.CancellationToken));
            Assert.Equal("Renewing", StoredPhase(directory.FullName, protector));
            await Assert.ThrowsAsync<SessionStateException>(() => new DeviceSessionCoordinator(api, vault).RestoreAsync(TestContext.Current.CancellationToken));
            Assert.Null(await new DeviceSessionCoordinator(api, vault).RestoreAsync(TestContext.Current.CancellationToken));
            Assert.Equal(1, renewals);
            Assert.Equal(1, revocations);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task FailedReplacementSaveNeverUsesNewAccessAndNeverReplaysOldRefresh()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-renew-save-fault-");
        using var protector = new TestSecretProtector();
        var vault = new ProtectedSessionVault(directory.FullName, protector);
        var calls = new List<string>();
        try
        {
            await SeedAsync(vault, SessionVaultTests.Record(SessionPhase.Active, Credentials(expired: true)));
            protector.FailOnProtect = 3; // Seed, durable renewing marker, then replacement.
            using var api = Api((request, _) =>
            {
                calls.Add(request.RequestUri!.AbsolutePath);
                return Task.FromResult(request.RequestUri.AbsolutePath.EndsWith("/renew", StringComparison.Ordinal)
                    ? Ok(Credentials(expired: false, marker: 'c')) : new HttpResponseMessage(HttpStatusCode.NoContent));
            });
            await Assert.ThrowsAsync<SessionStorageException>(() => new DeviceSessionCoordinator(api, vault).RestoreAsync(TestContext.Current.CancellationToken));
            Assert.Equal("Renewing", StoredPhase(directory.FullName, protector));
            protector.FailOnProtect = null;
            await Assert.ThrowsAsync<SessionStateException>(() => new DeviceSessionCoordinator(api, vault).RestoreAsync(TestContext.Current.CancellationToken));
            Assert.Equal(new[] { "/api/companion/session/renew", "/api/companion/session/revoke" }, calls);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task RedeemedCredentialsAreProtectedBeforeGrantReadAndCanRecoverAnOfflineVerification()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-verify-recovery-");
        using var protector = new TestSecretProtector();
        var vault = new ProtectedSessionVault(directory.FullName, protector);
        var credentials = Credentials(expired: false);
        var reads = 0;
        try
        {
            using var api = Api((_, _) =>
            {
                Assert.Equal("Verifying", StoredPhase(directory.FullName, protector));
                if (++reads == 1) { throw new HttpRequestException("Temporary outage."); }
                return Task.FromResult(Metadata(credentials.SessionId));
            });
            await Assert.ThrowsAsync<CompanionConnectionException>(() => new DeviceSessionCoordinator(api, vault).ConnectAsync(credentials, TestContext.Current.CancellationToken));
            Assert.Equal("private-home", (await new DeviceSessionCoordinator(api, vault).RestoreAsync(TestContext.Current.CancellationToken))!.HomeId);
            Assert.Equal("Active", StoredPhase(directory.FullName, protector));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task WrongEndpointNeverReceivesAStoredCredentialAndIdentityChangeIsRejected()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-session-identity-");
        using var protector = new TestSecretProtector();
        var vault = new ProtectedSessionVault(directory.FullName, protector);
        var calls = 0;
        try
        {
            await SeedAsync(vault, SessionVaultTests.Record(SessionPhase.Active));
            using var wrong = new CompanionApiClient(new Uri("https://other-host"), new ScriptedCompanionHandler((_, _) => { calls++; throw new InvalidOperationException(); }));
            Assert.Equal(SessionStateFailure.WrongEndpoint, (await Assert.ThrowsAsync<SessionStateException>(() => new DeviceSessionCoordinator(wrong, vault).RestoreAsync(TestContext.Current.CancellationToken))).Failure);
            Assert.Equal(0, calls);
            using var changed = Api((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/revoke", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.NoContent) : Metadata(new string('a', 32), "other-home")));
            Assert.Equal(SessionStateFailure.IdentityChanged, (await Assert.ThrowsAsync<SessionStateException>(() => new DeviceSessionCoordinator(changed, vault).RestoreAsync(TestContext.Current.CancellationToken))).Failure);
            Assert.Null(await new DeviceSessionCoordinator(changed, vault).RestoreAsync(TestContext.Current.CancellationToken));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task OfflineDisconnectClearsLocalSecretsWithoutClaimingServerRevocation()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-disconnect-");
        using var protector = new TestSecretProtector();
        var vault = new ProtectedSessionVault(directory.FullName, protector);
        try
        {
            await SeedAsync(vault, SessionVaultTests.Record(SessionPhase.Active, Credentials(expired: true)));
            using var api = Api((request, _) =>
            {
                Assert.EndsWith("/revoke", request.RequestUri!.AbsolutePath);
                Assert.False(File.Exists(Path.Combine(directory.FullName, "session.bin")));
                throw new HttpRequestException("Offline.");
            });
            var coordinator = new DeviceSessionCoordinator(api, vault);
            Assert.False(await coordinator.DisconnectAsync(TestContext.Current.CancellationToken));
            Assert.Null(await coordinator.RestoreAsync(TestContext.Current.CancellationToken));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task RevokedSessionClearsTheVaultRatherThanFallingBackToSamplesOrSavedAuthority()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-revoked-");
        using var protector = new TestSecretProtector();
        var vault = new ProtectedSessionVault(directory.FullName, protector);
        try
        {
            await SeedAsync(vault, SessionVaultTests.Record(SessionPhase.Active));
            using var api = Api((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
            var coordinator = new DeviceSessionCoordinator(api, vault);
            Assert.Equal(ConnectionFailure.Unauthorized, (await Assert.ThrowsAsync<CompanionConnectionException>(() => coordinator.RestoreAsync(TestContext.Current.CancellationToken))).Failure);
            Assert.Null(await coordinator.RestoreAsync(TestContext.Current.CancellationToken));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task CancellationDuringRenewalKeepsTheUnsafeMarkerForRestart()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-renew-cancel-");
        using var protector = new TestSecretProtector();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var vault = new ProtectedSessionVault(directory.FullName, protector);
        try
        {
            await SeedAsync(vault, SessionVaultTests.Record(SessionPhase.Active, Credentials(expired: true)));
            using var api = Api((_, token) => { cancellation.Cancel(); throw new OperationCanceledException(token); });
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DeviceSessionCoordinator(api, vault).RestoreAsync(cancellation.Token));
            Assert.Equal("Renewing", StoredPhase(directory.FullName, protector));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task MarkerProtectionFailurePreventsAnyRefreshRequest()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-renew-preflight-");
        using var protector = new TestSecretProtector();
        var vault = new ProtectedSessionVault(directory.FullName, protector);
        var calls = 0;
        try
        {
            await SeedAsync(vault, SessionVaultTests.Record(SessionPhase.Active, Credentials(expired: true)));
            protector.FailOnProtect = 2;
            using var api = Api((_, _) => { calls++; throw new InvalidOperationException(); });
            await Assert.ThrowsAsync<SessionStorageException>(() => new DeviceSessionCoordinator(api, vault).RestoreAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, calls);
            Assert.Equal("Active", StoredPhase(directory.FullName, protector));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task OfflineVerificationCanRenewLaterWithoutInventingAHomeIdentity()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-delayed-verification-");
        using var protector = new TestSecretProtector();
        var vault = new ProtectedSessionVault(directory.FullName, protector);
        try
        {
            await SeedAsync(vault, SessionVaultTests.Record(SessionPhase.Verifying, Credentials(expired: true)));
            using var api = Api((request, _) =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/renew", StringComparison.Ordinal))
                { Assert.Equal("Renewing", StoredPhase(directory.FullName, protector)); return Task.FromResult(Ok(Credentials(expired: false, marker: 'c'))); }
                Assert.Equal("Verifying", StoredPhase(directory.FullName, protector));
                return Task.FromResult(Metadata(new string('a', 32)));
            });
            var session = await new DeviceSessionCoordinator(api, vault).RestoreAsync(TestContext.Current.CancellationToken);
            Assert.Equal("private-home", session!.HomeId);
            Assert.Equal("Active", StoredPhase(directory.FullName, protector));
        }
        finally { directory.Delete(recursive: true); }
    }

    private static CompanionApiClient Api(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => new(new Uri("https://localhost"), new ScriptedCompanionHandler(send));
    private static HttpResponseMessage Ok(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
    private static HttpResponseMessage Metadata(string sessionId, string homeId = "private-home") => Ok(new
    { sessionId, deviceName = "Phone", expiresAt = DateTimeOffset.UtcNow.AddDays(30), homeId, memberId = "member-a", places = Array.Empty<object>() });
    private static DeviceCredentials Credentials(bool expired, char marker = 'a') => new(new string('a', 32), "lca_" + new string(marker, 43),
        DateTimeOffset.UtcNow.AddMinutes(expired ? -1 : 5), "lcr_" + new string(marker, 43), DateTimeOffset.UtcNow.AddDays(7));
    private static async Task SeedAsync(ISessionVault vault, StoredDeviceSession record)
    {
        await using var lease = await vault.OpenAsync(TestContext.Current.CancellationToken);
        await lease.SaveAsync(record, TestContext.Current.CancellationToken);
    }
    private static string StoredPhase(string directory, TestSecretProtector protector)
    {
        var plaintext = protector.Unprotect(File.ReadAllBytes(Path.Combine(directory, "session.bin")));
        try { using var document = System.Text.Json.JsonDocument.Parse(plaintext); return document.RootElement.GetProperty("phase").GetString()!; }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(plaintext); }
    }
}

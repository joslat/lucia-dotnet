using System.Net;
using System.Net.Http.Json;
using Lucia.Companion.Connection;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class CompanionConnectionTests
{
    [Fact]
    public async Task PairingReturnsOnlyDisplayCodeAndProtectsCredentialsBeforeReturningConnection()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-ui-connection-");
        using var protector = new TestSecretProtector();
        var vault = new ProtectedSessionVault(directory.FullName, protector);
        try
        {
            using var connection = new CompanionConnection(vault, endpoint => new(endpoint, new ScriptedCompanionHandler((request, _) =>
            {
                var path = request.RequestUri!.AbsolutePath;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = path switch
                {
                    "/api/companion/pairings" => JsonContent.Create(new { deviceCode = "lcp_" + new string('a', 43), userCode = "ABCDE-FGHJK", expiresAt = DateTimeOffset.UtcNow.AddMinutes(10), pollIntervalSeconds = 5 }),
                    "/api/companion/pairings/redeem" => JsonContent.Create(SessionVaultTests.Record(SessionPhase.Active).Credentials),
                    _ => JsonContent.Create(new { sessionId = new string('a', 32), deviceName = "Phone", expiresAt = DateTimeOffset.UtcNow.AddDays(30), homeId = "private-home", memberId = "member-a", places = Array.Empty<object>() })
                } });
            })));
            var prompt = await connection.BeginAsync(new Uri("https://localhost"), "Phone", TestContext.Current.CancellationToken);
            Assert.Equal("ABCDE-FGHJK", prompt.UserCode);
            Assert.DoesNotContain("ABCDE-FGHJK", prompt.ToString());
            Assert.DoesNotContain(prompt.GetType().GetProperties(), property => property.Name.Contains("Token", StringComparison.Ordinal) || property.Name == "DeviceCode");
            var approved = await connection.PollAsync(TestContext.Current.CancellationToken);
            Assert.Equal(PairingPollStatus.Success, approved.Status);
            Assert.Equal("private-home", approved.Connection!.Session.HomeId);
            Assert.True(connection.HasSavedConnection);
            await using var lease = await vault.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal(SessionPhase.Active, (await lease.ReadAsync(TestContext.Current.CancellationToken))!.Phase);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task AnotherInstanceRemovingTheRecordDuringRestoreDoesNotLeavePairingDisabled()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-ui-restore-race-");
        using var protector = new TestSecretProtector();
        var vault = new ProtectedSessionVault(directory.FullName, protector);
        try
        {
            await using (var lease = await vault.OpenAsync(TestContext.Current.CancellationToken))
            { await lease.SaveAsync(SessionVaultTests.Record(SessionPhase.Active), TestContext.Current.CancellationToken); }
            using var connection = new CompanionConnection(vault, endpoint =>
            {
                File.Delete(Path.Combine(directory.FullName, "session.bin"));
                return new(endpoint, new ScriptedCompanionHandler((_, _) => throw new InvalidOperationException("No credentials should be sent.")));
            });
            Assert.Null(await connection.RestoreAsync(TestContext.Current.CancellationToken));
            Assert.False(connection.HasSavedConnection);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task UntrustedRenewalIdentityKeepsRecoveryEnabledForItsDurableMarker()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-ui-renewal-identity-");
        using var protector = new TestSecretProtector();
        var vault = new ProtectedSessionVault(directory.FullName, protector);
        try
        {
            var expiredAccess = new DeviceCredentials(new string('a', 32), "lca_" + new string('a', 43), DateTimeOffset.UtcNow.AddMinutes(-1), "lcr_" + new string('b', 43), DateTimeOffset.UtcNow.AddDays(7));
            await using (var lease = await vault.OpenAsync(TestContext.Current.CancellationToken))
            { await lease.SaveAsync(SessionVaultTests.Record(SessionPhase.Active, expiredAccess), TestContext.Current.CancellationToken); }
            using var connection = new CompanionConnection(vault, endpoint => new(endpoint, new ScriptedCompanionHandler((_, _) => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new DeviceCredentials(new string('c', 32), "lca_" + new string('c', 43), DateTimeOffset.UtcNow.AddMinutes(5), "lcr_" + new string('d', 43), DateTimeOffset.UtcNow.AddDays(7))) }))));
            await Assert.ThrowsAsync<SessionStateException>(() => connection.RestoreAsync(TestContext.Current.CancellationToken));
            Assert.True(connection.HasSavedConnection);
            await using var preserved = await vault.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal(SessionPhase.Renewing, (await preserved.ReadAsync(TestContext.Current.CancellationToken))!.Phase);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task ExistingProtectedConnectionCannotBeReplacedByStartingAnotherHost()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-ui-existing-");
        using var protector = new TestSecretProtector();
        var vault = new ProtectedSessionVault(directory.FullName, protector);
        try
        {
            await using (var lease = await vault.OpenAsync(TestContext.Current.CancellationToken))
            { await lease.SaveAsync(SessionVaultTests.Record(SessionPhase.Active), TestContext.Current.CancellationToken); }
            using var connection = new CompanionConnection(vault, _ => throw new InvalidOperationException("No network client should be created."));
            await Assert.ThrowsAsync<SessionStateException>(() => connection.BeginAsync(new Uri("https://another-host"), "Phone", TestContext.Current.CancellationToken));
            Assert.True(connection.HasSavedConnection);
            await using var preserved = await vault.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal("private-home", (await preserved.ReadAsync(TestContext.Current.CancellationToken))!.HomeId);
        }
        finally { directory.Delete(recursive: true); }
    }
}

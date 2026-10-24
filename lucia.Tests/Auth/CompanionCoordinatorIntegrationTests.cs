using FakeItEasy;
using Lucia.Companion.Connection;
using lucia.Agents.Companion;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Time.Testing;

namespace lucia.Tests.Auth;

public sealed class CompanionCoordinatorIntegrationTests
{
    [Fact]
    public async Task NativeCoordinatorProtectsRestoresRenewsAndRemovesRevokedSessionOverRealHttps()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var host = await CompanionSecurityTestHost.StartAsync(clock);
        using var api = host.CreateNativeClient();
        var directory = Directory.CreateTempSubdirectory("lucia-native-session-e2e-");
        try
        {
            var vault = new ProtectedSessionVault(directory.FullName, EphemeralProtector());
            var challenge = await api.StartPairingAsync("Synthetic phone");
            var review = (await host.Pairings.ReviewAsync(challenge.UserCode))!;
            await host.Pairings.ApproveAsync(review.Id, new("home-a", "member-a", [new("living", [CompanionField.Temperature])]), "owner-a");
            var credentials = (await api.PollPairingAsync(challenge.DeviceCode)).Credentials!;
            var coordinator = new DeviceSessionCoordinator(api, vault, clock);
            Assert.Equal("home-a", (await coordinator.ConnectAsync(credentials)).HomeId);
            clock.Advance(TimeSpan.FromMinutes(6));
            var results = await Task.WhenAll(coordinator.RestoreAsync(), new DeviceSessionCoordinator(api, vault, clock).RestoreAsync());
            Assert.All(results, session => Assert.Equal(credentials.SessionId, session!.SessionId));
            Assert.Null(await host.Sessions.AuthenticateAsync(credentials.AccessToken));
            Assert.True(await host.Sessions.RevokeAsync(credentials.SessionId, "home-a"));
            Assert.Equal(ConnectionFailure.Unauthorized, (await Assert.ThrowsAsync<CompanionConnectionException>(() => coordinator.RestoreAsync())).Failure);
            Assert.Null(await coordinator.RestoreAsync());
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task UiSafeConnectionPairsProtectsRestoresAndDisconnectsWithoutExposingTokens()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        var directory = Directory.CreateTempSubdirectory("lucia-ui-safe-e2e-");
        try
        {
            var vault = new ProtectedSessionVault(directory.FullName, EphemeralProtector());
            using var connection = new CompanionConnection(vault, endpoint =>
            {
                Assert.Equal(host.Client.BaseAddress, endpoint);
                return host.CreateNativeClient();
            });
            var prompt = await connection.BeginAsync(host.Client.BaseAddress!, "Synthetic phone");
            var intent = (await host.Pairings.ReviewAsync(prompt.UserCode))!;
            Assert.True(await host.Pairings.ApproveAsync(intent.Id, new("home-a", "member-a", [new("porch", [CompanionField.Contact])]), "owner-a"));
            var paired = await connection.PollAsync();
            Assert.Equal(PairingPollStatus.Success, paired.Status);
            Assert.Equal("home-a", paired.Connection!.Session.HomeId);
            Assert.True(connection.HasSavedConnection);
            Assert.Equal(paired.Connection.Session.SessionId, (await connection.RestoreAsync())!.Session.SessionId);
            Assert.True(await connection.DisconnectAsync());
            Assert.False(connection.HasSavedConnection);
            Assert.Empty(await host.Sessions.ListActiveAsync("home-a"));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task CrashRecoveryRevokesARealRotationWhoseReplyWasLostWithoutReplayingRefresh()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        using var api = host.CreateNativeClient();
        var directory = Directory.CreateTempSubdirectory("lucia-native-rotation-e2e-");
        try
        {
            var vault = new ProtectedSessionVault(directory.FullName, EphemeralProtector());
            var issued = await host.Sessions.CreateAsync(new("home-a", "member-a", []), "Synthetic phone");
            var original = new DeviceCredentials(issued.SessionId, issued.AccessToken, issued.AccessExpiresAt, issued.RefreshToken, issued.RefreshExpiresAt);
            await using (var lease = await vault.OpenAsync())
            { await lease.SaveAsync(new(1, api.Endpoint, original, SessionPhase.Renewing, "home-a", "member-a")); }
            // The server committed; the app's durable state still says it never received the replacement.
            var orphaned = await api.RenewAsync(original.RefreshToken);
            await Assert.ThrowsAsync<SessionStateException>(() => new DeviceSessionCoordinator(api, vault).RestoreAsync());
            Assert.Null(await host.Sessions.AuthenticateAsync(orphaned.AccessToken));
            Assert.Null(await new DeviceSessionCoordinator(api, vault).RestoreAsync());
        }
        finally { directory.Delete(recursive: true); }
    }

    private static IDeviceSecretProtector EphemeralProtector()
    {
        // Integration-only in-memory key. Platform DPAPI/Keystore require their own target checks.
        var ephemeral = new EphemeralDataProtectionProvider().CreateProtector("Lucia.SyntheticDeviceVault");
        var protector = A.Fake<IDeviceSecretProtector>();
        A.CallTo(() => protector.Protect(A<byte[]>._)).ReturnsLazily((byte[] bytes) => ephemeral.Protect(bytes));
        A.CallTo(() => protector.Unprotect(A<byte[]>._)).ReturnsLazily((byte[] bytes) => ephemeral.Unprotect(bytes));
        return protector;
    }
}

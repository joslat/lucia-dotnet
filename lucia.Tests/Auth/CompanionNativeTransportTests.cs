using Lucia.Companion.Connection;
using lucia.Agents.Companion;

namespace lucia.Tests.Auth;

public sealed class CompanionNativeTransportTests
{
    [Fact]
    public async Task ActualNativeClientPairsLearnsGrantRenewsAndLogsOutOverHttps()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        using var device = host.CreateNativeClient();
        var challenge = await device.StartPairingAsync("Synthetic native client");
        var intent = (await host.Pairings.ReviewAsync(challenge.UserCode))!;
        Assert.True(await host.Pairings.ApproveAsync(intent.Id,
            new("home-a", "member-a", [new("porch", [CompanionField.Contact, CompanionField.Motion])]), "owner-a"));
        var approved = await device.PollPairingAsync(challenge.DeviceCode);
        Assert.Equal(PairingPollStatus.Success, approved.Status);
        var credentials = Assert.IsType<DeviceCredentials>(approved.Credentials);
        var metadata = await device.GetSessionAsync(credentials.AccessToken);
        Assert.Equal("home-a", metadata.HomeId);
        Assert.Equal("member-a", metadata.MemberId);
        Assert.Equal("porch", Assert.Single(metadata.Places).PlaceId);
        Assert.False(metadata.CanReadEngine);
        Assert.Equal(credentials.SessionId, metadata.SessionId);
        var rotated = await device.RenewAsync(credentials.RefreshToken);
        Assert.NotEqual(credentials.AccessToken, rotated.AccessToken);
        var oldAccess = await Assert.ThrowsAsync<CompanionConnectionException>(() => device.GetSessionAsync(credentials.AccessToken));
        Assert.Equal(ConnectionFailure.Unauthorized, oldAccess.Failure);
        Assert.Equal(metadata.SessionId, (await device.GetSessionAsync(rotated.AccessToken)).SessionId);
        await device.LogoutAsync(rotated.AccessToken);
        Assert.Equal(ConnectionFailure.Unauthorized, (await Assert.ThrowsAsync<CompanionConnectionException>(() => device.GetSessionAsync(rotated.AccessToken))).Failure);
        Assert.Equal(ConnectionFailure.Unauthorized, (await Assert.ThrowsAsync<CompanionConnectionException>(() => device.RenewAsync(rotated.RefreshToken))).Failure);
    }

    [Fact]
    public async Task ActualNativeClientDistinguishesPendingThrottleAndOwnerDenial()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        using var device = host.CreateNativeClient();
        var challenge = await device.StartPairingAsync("Synthetic phone");
        Assert.Equal(PairingPollStatus.Pending, (await device.PollPairingAsync(challenge.DeviceCode)).Status);
        Assert.Equal(PairingPollStatus.SlowDown, (await device.PollPairingAsync(challenge.DeviceCode)).Status);
        var review = (await host.Pairings.ReviewAsync(challenge.UserCode))!;
        Assert.True(await host.Pairings.CancelAsync(review.Id));
        Assert.Equal(PairingPollStatus.Denied, (await device.PollPairingAsync(challenge.DeviceCode)).Status);
    }

    [Fact]
    public async Task ProductionClientDoesNotTrustTheTestCertificate()
    {
        await using var host = await CompanionSecurityTestHost.StartAsync();
        using var device = new CompanionApiClient(host.Client.BaseAddress!);
        var error = await Assert.ThrowsAsync<CompanionConnectionException>(() => device.StartPairingAsync("Phone"));
        Assert.Equal(ConnectionFailure.Transport, error.Failure);
    }
}

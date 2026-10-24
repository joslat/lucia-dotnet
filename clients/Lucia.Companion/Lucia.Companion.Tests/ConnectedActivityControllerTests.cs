using System.Net;
using Lucia.Companion.App.Configuration;
using Lucia.Companion.Connection;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class ConnectedActivityControllerTests
{
    [Fact]
    public async Task NoActivityGrantMakesNoRequestAndDoesNotPretendLuciaIsIdle()
    {
        var service = new TestCompanionConnection { Activity = () => throw new InvalidOperationException("Not authorized to send.") };
        using var controller = new ConnectedActivityController(service, TestCompanionConnection.Session());
        await controller.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.True(controller.AccessDenied); Assert.False(controller.AuthorityLost); Assert.False(controller.CanRefresh);
        Assert.Null(controller.Snapshot);
        Assert.Contains("not approved", controller.StatusText, StringComparison.OrdinalIgnoreCase);
        controller.Suspend(); await controller.ResumeAsync(TestContext.Current.CancellationToken);
        Assert.Contains("not approved", controller.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifiedActivityUsesTheExactMemberAndKeepsReportedStateAndTimes()
    {
        var snapshot = Snapshot();
        using var controller = new ConnectedActivityController(Service(() => Task.FromResult(snapshot)), Identity());
        await controller.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Same(snapshot, controller.Snapshot);
        Assert.Equal(RequestActivityState.Working, Assert.Single(controller.Snapshot!.Requests).State);
        Assert.False(controller.IsBusy); Assert.False(controller.AccessDenied); Assert.False(controller.AuthorityLost);
        Assert.DoesNotContain("idle", controller.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("home")]
    [InlineData("member")]
    public async Task AProjectionCannotCrossTheViewIdentityEvenIfAServiceAdapterFailsToValidateIt(string mismatch)
    {
        var clock = new MutableConnectionClock(); var reads = 0;
        var wrong = new ActivitySnapshot(mismatch == "home" ? "home-b" : "home-a", mismatch == "member" ? "member-b" : "member-a",
            clock.Now, clock.Now, true, false, false, []);
        using var controller = new ConnectedActivityController(Service(() => { reads++; return Task.FromResult(wrong); }), Identity(), clock);
        await controller.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Null(controller.Snapshot); Assert.False(controller.AuthorityLost);
        clock.TimestampSeconds = 1000; await controller.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task AnUnavailableFeedClearsActivityAndRetriesWithoutOwningAnyHomeOrSetupState()
    {
        var clock = new MutableConnectionClock(); var reads = 0;
        var service = Service(() => { reads++; return Task.FromResult(Snapshot()); });
        using var controller = new ConnectedActivityController(service, Identity(), clock);
        await controller.RefreshAsync(TestContext.Current.CancellationToken);
        service.Activity = () => { reads++; throw new CompanionConnectionException(ConnectionFailure.Rejected, HttpStatusCode.ServiceUnavailable); };
        await controller.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Null(controller.Snapshot); Assert.False(controller.AuthorityLost);
        clock.TimestampSeconds = 19; await controller.TickAsync(TestContext.Current.CancellationToken); Assert.Equal(2, reads);
        service.Activity = () => { reads++; return Task.FromResult(Snapshot()); };
        clock.TimestampSeconds = 20; await controller.TickAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(controller.Snapshot); Assert.Equal(3, reads);
    }

    [Theory]
    [InlineData("activity-only", false)]
    [InlineData("revoked", true)]
    [InlineData("identity", true)]
    [InlineData("invalid-contract", false)]
    public async Task PermanentFailuresStayQuietAndSeparateActivityOnlyFromConnectionRecovery(string failure, bool authorityLost)
    {
        var clock = new MutableConnectionClock(); var reads = 0;
        using var controller = new ConnectedActivityController(Service(() =>
        {
            reads++;
            throw failure switch
            {
                "activity-only" => new ActivityAccessException(),
                "revoked" => new CompanionConnectionException(ConnectionFailure.Unauthorized, HttpStatusCode.Unauthorized),
                "identity" => new SessionStateException(SessionStateFailure.IdentityChanged),
                _ => new CompanionConnectionException(ConnectionFailure.InvalidResponse)
            };
        }), Identity(), clock);
        await controller.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Null(controller.Snapshot); Assert.Equal(authorityLost, controller.AuthorityLost);
        Assert.Equal(failure == "activity-only", controller.AccessDenied);
        clock.TimestampSeconds = 1000; await controller.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task ConcurrentTicksShareOneProtectedReadAndSuspensionDiscardsItsLateResult()
    {
        var pending = new TaskCompletionSource<ActivitySnapshot>(TaskCreationOptions.RunContinuationsAsynchronously); var reads = 0;
        var service = Service(() => { reads++; return pending.Task; });
        using var controller = new ConnectedActivityController(service, Identity());
        var first = controller.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.True(controller.IsBusy); Assert.Same(first, controller.RefreshAsync(TestContext.Current.CancellationToken));
        controller.Suspend(); pending.SetResult(Snapshot()); await first;
        Assert.Null(controller.Snapshot); Assert.Equal(1, reads);
        service.Activity = () => { reads++; return Task.FromResult(Snapshot()); };
        await controller.ResumeAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(controller.Snapshot); Assert.Equal(2, reads);
    }

    [Fact]
    public async Task RetryAfterSurvivesManualRefreshAndSuspendWithoutReplayingBeforeTheServerWait()
    {
        var clock = new MutableConnectionClock(); var reads = 0;
        var service = Service(() => { reads++; throw new CompanionConnectionException(ConnectionFailure.Rejected, HttpStatusCode.TooManyRequests, TimeSpan.FromMinutes(2)); });
        using var controller = new ConnectedActivityController(service, Identity(), clock);
        await controller.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.False(controller.CanRefresh);
        clock.TimestampSeconds = 60;
        await controller.RefreshAsync(TestContext.Current.CancellationToken); controller.Suspend(); await controller.ResumeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, reads);
        service.Activity = () => { reads++; return Task.FromResult(Snapshot()); };
        clock.TimestampSeconds = 120; await controller.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, reads); Assert.NotNull(controller.Snapshot);
    }

    [Fact]
    public async Task ConfiguredReadsPollOnMonotonicTimeButUnconfiguredCollectionDoesNotLoop()
    {
        var clock = new MutableConnectionClock(); var reads = 0;
        var service = Service(() => { reads++; return Task.FromResult(Snapshot()); });
        using var controller = new ConnectedActivityController(service, Identity(), clock);
        await controller.RefreshAsync(TestContext.Current.CancellationToken);
        clock.Now = clock.Now.AddYears(-10); clock.TimestampSeconds = 4;
        await controller.TickAsync(TestContext.Current.CancellationToken); Assert.Equal(1, reads);
        clock.TimestampSeconds = 5; await controller.TickAsync(TestContext.Current.CancellationToken); Assert.Equal(2, reads);
        service.Activity = () => { reads++; var now = DateTimeOffset.UtcNow; return Task.FromResult(new ActivitySnapshot("home-a", "member-a", now, now, false, false, false, [])); };
        await controller.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.False(controller.Snapshot!.IsConfigured);
        clock.TimestampSeconds = 1000; await controller.TickAsync(TestContext.Current.CancellationToken); Assert.Equal(3, reads);
        Assert.Contains("not configured", controller.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AThrottleArrivingAfterSuspensionStillSchedulesTheFirstAutomaticRecovery()
    {
        var clock = new MutableConnectionClock(); var reads = 0;
        var pending = new TaskCompletionSource<ActivitySnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = Service(() => { reads++; return pending.Task; });
        using var controller = new ConnectedActivityController(service, Identity(), clock);
        var first = controller.RefreshAsync(TestContext.Current.CancellationToken);
        controller.Suspend(); clock.TimestampSeconds = 10;
        pending.SetException(new CompanionConnectionException(ConnectionFailure.Rejected, HttpStatusCode.TooManyRequests, TimeSpan.FromMinutes(2)));
        await first;
        clock.TimestampSeconds = 20; await controller.ResumeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, reads); Assert.Null(controller.Snapshot);
        service.Activity = () => { reads++; return Task.FromResult(Snapshot()); };
        clock.TimestampSeconds = 129; await controller.TickAsync(TestContext.Current.CancellationToken); Assert.Equal(1, reads);
        clock.TimestampSeconds = 130; await controller.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, reads); Assert.NotNull(controller.Snapshot);
    }

    [Theory]
    [InlineData("older-revision")]
    [InlineData("same-revision-change")]
    [InlineData("backward-phase")]
    public async Task ARepeatedRequestCannotRegressOrChangeWithoutARevision(string defect)
    {
        var original = Snapshot(); var old = original.Requests[0];
        var service = Service(() => Task.FromResult(original));
        using var controller = new ConnectedActivityController(service, Identity());
        await controller.RefreshAsync(TestContext.Current.CancellationToken);
        var updatedAt = old.UpdatedAt.AddMilliseconds(1);
        var next = new ActivityRequest(old.RequestId, defect == "backward-phase" ? RequestActivityState.Received : RequestActivityState.ResponseReady,
            old.StartedAt, updatedAt, defect == "older-revision" ? old.Revision - 1 : defect == "same-revision-change" ? old.Revision : old.Revision + 1);
        service.Activity = () => Task.FromResult(new ActivitySnapshot(original.HomeId, original.MemberId, original.CapturedAt.AddSeconds(1), original.CoverageStartedAt,
            true, false, false, [next]));
        await controller.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Null(controller.Snapshot); Assert.False(controller.AuthorityLost);
    }

    internal static ConnectionSession Identity() => new(new Uri("https://synthetic.invalid"), CompanionActivityClientTests.Session());

    [Fact]
    public async Task RepeatedTransientFailuresBackOffToFiveMinutesAndSuccessResetsThePollingCadence()
    {
        var clock = new MutableConnectionClock(); var reads = 0;
        var service = Service(() => { reads++; throw new CompanionConnectionException(ConnectionFailure.Transport); });
        using var controller = new ConnectedActivityController(service, Identity(), clock);
        await controller.RefreshAsync(TestContext.Current.CancellationToken);
        foreach (var delay in new[] { 20, 40, 80, 160, 300, 300 })
        {
            var previous = reads;
            clock.TimestampSeconds += delay - 1;
            await controller.TickAsync(TestContext.Current.CancellationToken); Assert.Equal(previous, reads);
            clock.TimestampSeconds++;
            await controller.TickAsync(TestContext.Current.CancellationToken); Assert.Equal(previous + 1, reads);
        }
        service.Activity = () => { reads++; return Task.FromResult(Snapshot()); };
        clock.TimestampSeconds += 300;
        await controller.TickAsync(TestContext.Current.CancellationToken);
        var afterRecovery = reads;
        clock.TimestampSeconds += 4;
        await controller.TickAsync(TestContext.Current.CancellationToken); Assert.Equal(afterRecovery, reads);
        clock.TimestampSeconds++;
        await controller.TickAsync(TestContext.Current.CancellationToken); Assert.Equal(afterRecovery + 1, reads);
    }
    internal static TestCompanionConnection Service(Func<Task<ActivitySnapshot>> read) => new() { Activity = read };
    internal static ActivitySnapshot Snapshot()
    {
        var now = DateTimeOffset.UtcNow;
        return new("home-a", "member-a", now, now.AddMinutes(-1), true, false, false,
            [new(new string('a', 32), RequestActivityState.Working, now.AddSeconds(-2), now.AddSeconds(-1), 2)]);
    }
}

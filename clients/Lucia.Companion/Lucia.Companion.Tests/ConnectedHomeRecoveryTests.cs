using System.Net;
using Lucia.Companion.App.Configuration;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class ConnectedHomeRecoveryTests
{
    [Fact]
    public async Task TemporaryFailureRetriesWithBoundedBackoffAndAnUnconditionalResyncThenRestoresTheNormalInterval()
    {
        var clock = new MutableConnectionClock();
        var service = ConnectedHomeControllerTests.Service();
        var state = State();
        var revisions = new List<string?>();
        var failures = 0;
        service.HomeUpdate = revision =>
        {
            revisions.Add(revision);
            if (revisions.Count is >= 2 and <= 4) { failures++; throw new CompanionConnectionException(ConnectionFailure.Transport); }
            return Task.FromResult(new HomeUpdate(HomeUpdateClientTests.Revision, ConnectedHomeControllerTests.Snapshot()));
        };
        using var controller = new ConnectedHomeController(service, TestCompanionConnection.Session(), state, new(state, null), clock);
        await controller.RefreshAsync(cancellationToken: TestContext.Current.CancellationToken);
        clock.TimestampSeconds = 20; await controller.TickAsync(TestContext.Current.CancellationToken);
        Assert.False(controller.HasData);
        Assert.Contains("automatically", controller.StatusText);
        foreach (var time in new[] { 39, 40, 79, 80, 159, 160 })
        { clock.TimestampSeconds = time; await controller.TickAsync(TestContext.Current.CancellationToken); }
        Assert.Equal(3, failures);
        Assert.Equal(new string?[] { null, HomeUpdateClientTests.Revision, null, null, null }, revisions);
        Assert.True(controller.HasData);
        clock.TimestampSeconds = 179; await controller.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(5, revisions.Count);
        clock.TimestampSeconds = 180; await controller.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(6, revisions.Count);
    }

    [Fact]
    public async Task InitialFailureAlsoRecoversAndRepeatedFailuresNeverRetryFasterThanFiveMinutesAtTheCap()
    {
        var clock = new MutableConnectionClock();
        var service = ConnectedHomeControllerTests.Service();
        var state = State(); var calls = 0;
        service.Home = () => { calls++; throw new CompanionConnectionException(ConnectionFailure.Rejected, HttpStatusCode.ServiceUnavailable); };
        using var controller = new ConnectedHomeController(service, TestCompanionConnection.Session(), state, new(state, null), clock);
        await controller.RefreshAsync(cancellationToken: TestContext.Current.CancellationToken);
        foreach (var time in new[] { 20, 60, 140, 300, 600, 900 })
        { clock.TimestampSeconds = time; await controller.TickAsync(TestContext.Current.CancellationToken); }
        Assert.Equal(7, calls);
        clock.TimestampSeconds = 1199; await controller.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(7, calls);
        service.Home = () => { calls++; return Task.FromResult(ConnectedHomeControllerTests.Snapshot()); };
        clock.TimestampSeconds = 1200; await controller.TickAsync(TestContext.Current.CancellationToken);
        Assert.True(controller.HasData);
        Assert.Equal(8, calls);
    }

    [Fact]
    public async Task ThrottleDelaySurvivesManualRetryAndSuspendResumeWithoutUsingTheWallClock()
    {
        var clock = new MutableConnectionClock();
        var service = ConnectedHomeControllerTests.Service();
        var state = State(); var calls = 0;
        service.Home = () =>
        {
            if (++calls == 1) { throw new CompanionConnectionException(ConnectionFailure.Rejected, HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(95)); }
            return Task.FromResult(ConnectedHomeControllerTests.Snapshot());
        };
        using var controller = new ConnectedHomeController(service, TestCompanionConnection.Session(), state, new(state, null), clock);
        await controller.RefreshAsync(cancellationToken: TestContext.Current.CancellationToken);
        await controller.RefreshAsync(cancellationToken: TestContext.Current.CancellationToken);
        controller.Suspend();
        clock.Now = clock.Now.AddYears(10);
        await controller.ResumeAsync(TestContext.Current.CancellationToken);
        clock.TimestampSeconds = 94; await controller.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, calls);
        clock.TimestampSeconds = 95; await controller.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, calls);
        Assert.True(controller.HasData);
    }

    [Theory]
    [InlineData(ConnectionFailure.Unauthorized, HttpStatusCode.Unauthorized)]
    [InlineData(ConnectionFailure.Rejected, HttpStatusCode.Forbidden)]
    [InlineData(ConnectionFailure.InvalidResponse, HttpStatusCode.OK)]
    [InlineData(ConnectionFailure.Rejected, HttpStatusCode.UnprocessableEntity)]
    public async Task PermanentFailuresClearReportsAndNeverRetryAutomatically(ConnectionFailure failure, HttpStatusCode status)
    {
        var clock = new MutableConnectionClock();
        var service = ConnectedHomeControllerTests.Service();
        var state = State(); var calls = 0;
        using var controller = new ConnectedHomeController(service, TestCompanionConnection.Session(), state, new(state, null), clock);
        await controller.RefreshAsync(cancellationToken: TestContext.Current.CancellationToken);
        service.Home = () => { calls++; throw new CompanionConnectionException(failure, status); };
        await controller.RefreshAsync(false, TestContext.Current.CancellationToken);
        clock.TimestampSeconds = 3600; await controller.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, calls);
        Assert.False(controller.HasData);
        Assert.Empty(state.SourceCatalog);
        Assert.All(state.Home.Places, place => Assert.Empty(place.Observations));
    }

    [Fact]
    public async Task AnUnchangedRevisionDoesNotRejuvenateReportsAndResumeAlwaysResynchronizes()
    {
        var clock = new MutableConnectionClock();
        var service = ConnectedHomeControllerTests.Service();
        var state = State(); var revisions = new List<string?>();
        var now = DateTimeOffset.UtcNow;
        var snapshot = new HomeSnapshot("home-a", now, 10,
            [new("porch", "Porch", [new("binary_sensor.door", "Door", ObservationRole.Contact)],
                [new("binary_sensor.door", ObservationRole.Contact, ObservationQuality.Current, now, null, false, null)])]);
        service.HomeUpdate = revision =>
        {
            revisions.Add(revision);
            return Task.FromResult(new HomeUpdate(HomeUpdateClientTests.Revision, revision is null ? snapshot : null));
        };
        using var controller = new ConnectedHomeController(service, TestCompanionConnection.Session(), state, new(state, null), clock);
        await controller.RefreshAsync(cancellationToken: TestContext.Current.CancellationToken);
        state.SelectPlace("porch");
        clock.TimestampSeconds = 20; await controller.TickAsync(TestContext.Current.CancellationToken);
        Assert.True(controller.HasData);
        Assert.Equal("porch", state.SelectedPlace!.Id);
        Assert.Equal(ObservationQuality.Stale, Assert.Single(Assert.Single(state.Home.Places).Observations).Quality);
        controller.Suspend();
        clock.TimestampSeconds = 1000; await controller.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, revisions.Count);
        await controller.ResumeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new string?[] { null, HomeUpdateClientTests.Revision, null }, revisions);
    }

    [Fact]
    public async Task AnUnchangedReplyWithoutAnInMemorySnapshotFailsClosed()
    {
        var service = ConnectedHomeControllerTests.Service();
        service.HomeUpdate = _ => Task.FromResult(new HomeUpdate(HomeUpdateClientTests.Revision, null));
        var state = State();
        using var controller = new ConnectedHomeController(service, TestCompanionConnection.Session(), state, new(state, null));
        await controller.RefreshAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(controller.HasData);
        Assert.Empty(state.SourceCatalog);
    }

    private static HomeViewModel State() => new(ConnectedHomeControllerTests.Setup(), isDemo: false);
}

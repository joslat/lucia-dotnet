using System.Net;
using Lucia.Companion.App.Configuration;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class ConnectedHomeControllerTests
{
    [Fact]
    public async Task VerifiedReportsProjectSavedMetadataWithoutWritingReadingsToTheProfile()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-home-controller-");
        try
        {
            var store = new LocalHomeStore(Path.Combine(directory.FullName, "connected.json"));
            var profile = HomeProfile.From(Setup(), []);
            await store.SaveAsync(profile, null, TestContext.Current.CancellationToken);
            var before = await File.ReadAllBytesAsync(Path.Combine(directory.FullName, "connected.json"), TestContext.Current.CancellationToken);
            var state = new HomeViewModel(new("home-a", "Your home", [], []), isDemo: false);
            var persistence = new HomeProfileSession(state, store);
            using var controller = new ConnectedHomeController(Service(), TestCompanionConnection.Session(), state, persistence);
            await controller.RefreshAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(controller.HasData);
            Assert.Equal("Front step", Assert.Single(state.Home.Places).Name);
            Assert.Equal("Closed", Assert.Single(Assert.Single(state.Home.Places).Observations).CurrentText);
            Assert.Equal("porch", Assert.Single(state.SourceCatalog).SourcePlaceId);
            Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(directory.FullName, "connected.json"), TestContext.Current.CancellationToken));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    [InlineData(HttpStatusCode.Unauthorized, true)]
    public async Task FailedRefreshClearsVisibleDataAndSeparatesSourceFailureFromLostAuthority(HttpStatusCode code, bool lostAuthority)
    {
        var service = Service();
        var state = new HomeViewModel(Setup(), isDemo: false);
        using var controller = new ConnectedHomeController(service, TestCompanionConnection.Session(), state, new(state, null));
        await controller.RefreshAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(controller.HasData);
        service.Home = () => throw new CompanionConnectionException(code == HttpStatusCode.Unauthorized ? ConnectionFailure.Unauthorized : ConnectionFailure.Rejected, code);
        await controller.RefreshAsync(false, TestContext.Current.CancellationToken);
        Assert.False(controller.HasData);
        Assert.Equal(lostAuthority, controller.AuthorityLost);
        Assert.Empty(state.SourceCatalog);
        Assert.All(state.Home.Places, place => Assert.Empty(place.Observations));
        Assert.DoesNotContain("sample", controller.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuspendingDoesNotCancelProtectedWorkButDiscardsItsResultAndResumeRevalidates()
    {
        var service = Service();
        var response = new TaskCompletionSource<HomeSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Home = () => response.Task;
        var state = new HomeViewModel(Setup(), isDemo: false);
        using var controller = new ConnectedHomeController(service, TestCompanionConnection.Session(), state, new(state, null));
        var pending = controller.RefreshAsync(cancellationToken: TestContext.Current.CancellationToken);
        controller.Suspend();
        response.SetResult(Snapshot());
        await pending;
        Assert.False(controller.HasData);
        Assert.Empty(state.SourceCatalog);
        service.Home = () => Task.FromResult(Snapshot());
        await controller.ResumeAsync(TestContext.Current.CancellationToken);
        Assert.True(controller.HasData);
        Assert.Equal("Closed", Assert.Single(Assert.Single(state.Home.Places).Observations).CurrentText);
    }

    [Fact]
    public async Task WallClockChangesDoNotPreventTheBoundedRefreshInterval()
    {
        var service = Service();
        var reads = 0;
        service.Home = () => { reads++; return Task.FromResult(Snapshot()); };
        var clock = new MutableConnectionClock();
        var state = new HomeViewModel(Setup(), isDemo: false);
        using var controller = new ConnectedHomeController(service, TestCompanionConnection.Session(), state, new(state, null), clock);
        await controller.RefreshAsync(cancellationToken: TestContext.Current.CancellationToken);
        clock.Now = clock.Now.AddYears(-10);
        clock.TimestampSeconds = 19;
        await controller.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, reads);
        clock.TimestampSeconds = 20;
        await controller.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, reads);
    }

    internal static HomeTopology Setup() => new("home-a", "Our home", [new("porch", "Front step", PlaceState.Unavailable, "Unknown", []) { Icon = PlaceIcon.Door }], [],
        [new("porch", "binary_sensor.door", ObservationRole.Contact, "porch")]);
    internal static HomeSnapshot Snapshot() => new("home-a", DateTimeOffset.UtcNow, 1800,
        [new("porch", "Porch", [new("binary_sensor.door", "Front door contact", ObservationRole.Contact)],
            [new("binary_sensor.door", ObservationRole.Contact, ObservationQuality.Current, DateTimeOffset.UtcNow.AddSeconds(-1), null, false, null)])]);
    internal static TestCompanionConnection Service() => new() { HasSavedConnection = true, Restore = () => Task.FromResult<ConnectionSession?>(TestCompanionConnection.Session()), Home = () => Task.FromResult(Snapshot()) };
}

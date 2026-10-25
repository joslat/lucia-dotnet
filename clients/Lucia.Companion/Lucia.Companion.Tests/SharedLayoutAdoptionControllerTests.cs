using System.Net;
using Lucia.Companion.App.Configuration;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class SharedLayoutAdoptionControllerTests
{
    [Fact]
    public async Task ReadingOnlyReviewsAndAddingRequiresAnotherProtectedExpectedIdentityRead()
    {
        var identity = Identity(); var original = Draft(); var reads = 0;
        var service = Service((expected, _) => { Assert.Same(identity, expected); reads++; return Task.FromResult<SharedHomeLayout?>(Layout()); });
        using var controller = new SharedLayoutAdoptionController(service, identity, () => original);
        Assert.Null(await controller.AddToDraftAsync(TestContext.Current.CancellationToken)); Assert.Equal(0, reads);
        await controller.ReadAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(controller.Plan); Assert.Equal(1, reads); Assert.Equal("Personal draft", original.Name);
        var accepted = await controller.AddToDraftAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, reads); Assert.Equal("Owner home", accepted!.Name); Assert.Null(controller.Plan);
        Assert.Equal("Personal draft", original.Name); Assert.Single(accepted.Bindings);
    }

    [Fact]
    public async Task ChangedSharedVersionRequiresAnotherVisibleReviewAndDoesNotReplaceTheDraft()
    {
        var layout = Layout(); var draft = Draft();
        var service = Service((_, _) => Task.FromResult<SharedHomeLayout?>(layout));
        using var controller = new SharedLayoutAdoptionController(service, Identity(), () => draft);
        await controller.ReadAsync(TestContext.Current.CancellationToken); layout = Layout('B');
        Assert.Null(await controller.AddToDraftAsync(TestContext.Current.CancellationToken)); Assert.Null(controller.Plan);
        Assert.Contains("changed", controller.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Personal draft", draft.Name);
        await controller.ReadAsync(TestContext.Current.CancellationToken); Assert.Equal(new string('B', 64), controller.Plan!.Revision);
        Assert.NotNull(await controller.AddToDraftAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unauthorized")]
    [InlineData("unavailable")]
    [InlineData("storage")]
    [InlineData("identity")]
    [InlineData("invalid")]
    public async Task FailedConfirmationHidesSharedMetadataAndKeepsTheLocalDraft(string defect)
    {
        var reads = 0; var draft = Draft();
        var service = Service((_, _) => ++reads == 1 ? Task.FromResult<SharedHomeLayout?>(Layout()) : defect switch
        {
            "missing" => Task.FromResult<SharedHomeLayout?>(null),
            "unauthorized" => throw new CompanionConnectionException(ConnectionFailure.Unauthorized),
            "unavailable" => throw new CompanionConnectionException(ConnectionFailure.Rejected, HttpStatusCode.ServiceUnavailable),
            "storage" => throw new SessionStorageException(),
            "identity" => throw new SessionStateException(SessionStateFailure.IdentityChanged),
            _ => throw new ArgumentException("PRIVATE server payload must not be displayed")
        });
        using var controller = new SharedLayoutAdoptionController(service, Identity(), () => draft);
        await controller.ReadAsync(TestContext.Current.CancellationToken); Assert.NotNull(controller.Plan);
        Assert.Null(await controller.AddToDraftAsync(TestContext.Current.CancellationToken)); Assert.Null(controller.Plan);
        Assert.DoesNotContain("PRIVATE", controller.StatusText);
        Assert.Equal("Personal draft", draft.Name); Assert.True(controller.CanRead);
    }

    [Fact]
    public async Task ChangesToTheDraftDuringConfirmationAreNotLost()
    {
        var draft = Draft(); var delayed = new TaskCompletionSource<SharedHomeLayout?>(); var reads = 0;
        var service = Service((_, _) => ++reads == 1 ? Task.FromResult<SharedHomeLayout?>(Layout()) : delayed.Task);
        using var controller = new SharedLayoutAdoptionController(service, Identity(), () => draft);
        await controller.ReadAsync(TestContext.Current.CancellationToken); var applying = controller.AddToDraftAsync(TestContext.Current.CancellationToken);
        draft = new(draft.Id, "New edit", draft.Places, draft.Connections, draft.Bindings);
        delayed.SetResult(Layout()); Assert.Null(await applying); Assert.Equal("New edit", draft.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidationOrDisposalCannotReturnLateMetadataOrApplyAnInFlightChoice(bool dispose)
    {
        var delayed = new TaskCompletionSource<SharedHomeLayout?>(); var reads = 0;
        var service = Service((_, _) => ++reads == 1 ? Task.FromResult<SharedHomeLayout?>(Layout()) : delayed.Task);
        using var controller = new SharedLayoutAdoptionController(service, Identity(), Draft);
        await controller.ReadAsync(TestContext.Current.CancellationToken); var applying = controller.AddToDraftAsync(TestContext.Current.CancellationToken);
        if (dispose) { controller.Dispose(); } else { controller.Invalidate(); }
        delayed.SetResult(Layout()); Assert.Null(await applying); Assert.Null(controller.Plan); Assert.False(controller.IsBusy);
    }

    [Fact]
    public async Task DoubleSubmissionMakesOnlyOneConfirmationRead()
    {
        var delayed = new TaskCompletionSource<SharedHomeLayout?>(); var reads = 0;
        using var controller = new SharedLayoutAdoptionController(Service((_, _) => ++reads == 1 ? Task.FromResult<SharedHomeLayout?>(Layout()) : delayed.Task), Identity(), Draft);
        await controller.ReadAsync(TestContext.Current.CancellationToken); var first = controller.AddToDraftAsync(TestContext.Current.CancellationToken);
        Assert.Null(await controller.AddToDraftAsync(TestContext.Current.CancellationToken)); Assert.Equal(2, reads);
        delayed.SetResult(Layout()); Assert.NotNull(await first);
    }

    [Fact]
    public async Task ServerWaitSurvivesInvalidationAndManualRetryWithoutWallClockDependence()
    {
        var clock = new MutableConnectionClock(); var reads = 0;
        var service = Service((_, _) => { reads++; throw new CompanionConnectionException(ConnectionFailure.Rejected, HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(60)); });
        using var controller = new SharedLayoutAdoptionController(service, Identity(), Draft, clock);
        await controller.ReadAsync(TestContext.Current.CancellationToken); controller.Invalidate(); await controller.ReadAsync(TestContext.Current.CancellationToken); Assert.Equal(1, reads);
        clock.TimestampSeconds = 59; await controller.ReadAsync(TestContext.Current.CancellationToken); Assert.Equal(1, reads);
        clock.TimestampSeconds = 60; await controller.ReadAsync(TestContext.Current.CancellationToken); Assert.Equal(2, reads);
    }

    [Fact]
    public async Task CallerCancellationCannotAcceptALateSuccessfulConfirmation()
    {
        var delayed = new TaskCompletionSource<SharedHomeLayout?>(); var reads = 0;
        using var controller = new SharedLayoutAdoptionController(Service((_, _) => ++reads == 1 ? Task.FromResult<SharedHomeLayout?>(Layout()) : delayed.Task), Identity(), Draft);
        await controller.ReadAsync(TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var applying = controller.AddToDraftAsync(cancellation.Token); cancellation.Cancel();
        delayed.SetResult(Layout()); Assert.Null(await applying); Assert.Null(controller.Plan);
    }

    internal static HomeTopology Draft() => new("home-a", "Personal draft", [new("local", "My porch", PlaceState.Unavailable, "Unknown", [])], []);
    internal static ConnectionSession Identity() => new(new Uri("https://synthetic.invalid/"), new DeviceSession(new string('a', 32), "Phone", DateTimeOffset.UtcNow.AddDays(1), "home-a", "member-a", [new("porch", [ObservationRole.Temperature])]));
    internal static SharedHomeLayout Layout(char revision = 'A') => new("home-a", "member-a", new string(revision, 64), DateTimeOffset.UtcNow, "Owner home", false,
        [new("shared-porch", "Front porch", PlaceIcon.Door, "porch")], [], [new("shared-porch", "porch", "sensor.porch_temperature", ObservationRole.Temperature)]);
    internal static TestCompanionConnection Service(Func<ConnectionSession, CancellationToken, Task<SharedHomeLayout?>> read) => new() { SharedLayout = read };
}

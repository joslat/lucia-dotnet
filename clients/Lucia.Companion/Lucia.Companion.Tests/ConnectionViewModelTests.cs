using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Connection;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class ConnectionViewModelTests
{
    [Fact]
    public async Task InvalidHttpIsRejectedAndBareHostUsesHttps()
    {
        var service = new TestCompanionConnection();
        var state = new ConnectionViewModel(service);
        await state.InitializeAsync(TestContext.Current.CancellationToken);
        state.HostAddress = "http://synthetic.invalid";
        await state.StartAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, service.BeginCalls);
        Assert.True(state.IsProblem);
        state.HostAddress = "synthetic.invalid:4321";
        await state.StartAsync(TestContext.Current.CancellationToken);
        Assert.Equal("https://synthetic.invalid:4321/", service.Endpoint!.AbsoluteUri);
        Assert.Equal("ABCDE-FGHJK", state.Prompt!.UserCode);
    }

    [Fact]
    public async Task PollingHonorsDelayAndPauseWithoutDiscardingAnInFlightApproval()
    {
        var service = new TestCompanionConnection();
        var clock = new MutableConnectionClock();
        var state = new ConnectionViewModel(service, clock) { HostAddress = "https://synthetic.invalid" };
        await state.InitializeAsync(TestContext.Current.CancellationToken);
        await state.StartAsync(TestContext.Current.CancellationToken);
        await state.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, service.PollCalls);
        clock.Now += TimeSpan.FromSeconds(6);
        var reply = new TaskCompletionSource<PairingUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Poll = () => reply.Task;
        var checking = state.TickAsync(TestContext.Current.CancellationToken);
        state.StopWaiting();
        Assert.False(state.IsWaiting);
        reply.SetResult(new(PairingPollStatus.Success, TimeSpan.Zero, TestCompanionConnection.Session()));
        await checking;
        Assert.True(state.IsConnected);
        Assert.Null(state.Prompt);
        Assert.Contains("completed", state.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OfflineRestoreShowsRecoveryWithoutPretendingConnectedOrStartingPairing()
    {
        var service = new TestCompanionConnection { HasSavedConnection = true, Restore = () => throw new CompanionConnectionException(ConnectionFailure.Transport) };
        var state = new ConnectionViewModel(service);
        await state.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ConnectionViewStage.Recovery, state.Stage);
        Assert.Null(state.Connection);
        Assert.False(state.CanStart);
        Assert.True(state.CanForget);
    }

    [Fact]
    public async Task OfflineDisconnectClearlyReportsLocalOnlyRemoval()
    {
        var service = new TestCompanionConnection { HasSavedConnection = true, Restore = () => Task.FromResult<ConnectionSession?>(TestCompanionConnection.Session()), Disconnect = () => Task.FromResult(false) };
        var state = new ConnectionViewModel(service);
        await state.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True(state.IsConnected);
        await state.DisconnectAsync(TestContext.Current.CancellationToken);
        Assert.False(state.IsConnected);
        Assert.Contains("only", state.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.True(state.CanStart);
    }

    [Fact]
    public async Task ServerRetryDelayAndPausedWaitingDoNotSendExtraPolls()
    {
        var service = new TestCompanionConnection { Poll = () => Task.FromResult(new PairingUpdate(PairingPollStatus.SlowDown, TimeSpan.FromSeconds(20))) };
        var clock = new MutableConnectionClock();
        var state = new ConnectionViewModel(service, clock) { HostAddress = "synthetic.invalid" };
        await state.InitializeAsync(TestContext.Current.CancellationToken);
        await state.StartAsync(TestContext.Current.CancellationToken);
        clock.Now += TimeSpan.FromSeconds(6);
        await state.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, service.PollCalls);
        clock.Now += TimeSpan.FromSeconds(10);
        await state.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, service.PollCalls);
        state.StopWaiting();
        clock.Now += TimeSpan.FromSeconds(30);
        await state.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, service.PollCalls);
        state.ResumeWaiting();
        await state.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, service.PollCalls);
    }

    [Fact]
    public async Task VerifiedRestoreClearsAnyOlderApprovalPrompt()
    {
        var service = new TestCompanionConnection();
        var state = new ConnectionViewModel(service) { HostAddress = "synthetic.invalid" };
        await state.InitializeAsync(TestContext.Current.CancellationToken);
        await state.StartAsync(TestContext.Current.CancellationToken);
        state.StopWaiting();
        service.HasSavedConnection = true;
        service.Restore = () => Task.FromResult<ConnectionSession?>(TestCompanionConnection.Session());
        await state.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True(state.IsConnected);
        Assert.Null(state.Prompt);
        Assert.False(state.IsWaiting);
    }

    [Fact]
    public async Task UnsupportedPlatformOffersNoPlaintextConnectionFallback()
    {
        var state = new ConnectionViewModel(null);
        await state.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ConnectionViewStage.Unavailable, state.Stage);
        Assert.False(state.CanStart);
    }
}

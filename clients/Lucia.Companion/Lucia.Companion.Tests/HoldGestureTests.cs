using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class HoldGestureTests
{
    private static readonly TimeSpan Hold = TimeSpan.FromMilliseconds(450);

    [Fact]
    public void HoldingLongEnoughWithoutMovingStartsTheGesture()
    {
        var gesture = new HoldGesture(Hold, slop: 10);
        gesture.Press(100, 100, TimeSpan.Zero);
        Assert.Equal(HoldGestureState.Pending, gesture.State);
        Assert.False(gesture.Tick(TimeSpan.FromMilliseconds(300)));
        Assert.True(gesture.Tick(TimeSpan.FromMilliseconds(450)));
        Assert.Equal(HoldGestureState.Holding, gesture.State);
        Assert.False(gesture.Tick(TimeSpan.FromMilliseconds(900)));
    }

    [Fact]
    public void MovingBeyondTheSlopBeforeTheHoldCancelsIt()
    {
        var gesture = new HoldGesture(Hold, slop: 10);
        gesture.Press(100, 100, TimeSpan.Zero);
        Assert.True(gesture.Move(106, 106));
        Assert.Equal(HoldGestureState.Pending, gesture.State);
        Assert.False(gesture.Move(112, 100));
        Assert.Equal(HoldGestureState.Idle, gesture.State);
        Assert.False(gesture.Tick(Hold));
    }

    [Fact]
    public void MovingAfterTheHoldKeepsItAndReleaseReportsWhatWasHappening()
    {
        var gesture = new HoldGesture(Hold, slop: 10);
        gesture.Press(0, 0, TimeSpan.Zero);
        gesture.Tick(Hold);
        Assert.True(gesture.Move(200, 200));
        Assert.Equal(HoldGestureState.Holding, gesture.Release());
        Assert.Equal(HoldGestureState.Idle, gesture.State);

        gesture.Press(0, 0, TimeSpan.Zero);
        Assert.Equal(HoldGestureState.Pending, gesture.Release());
        Assert.Equal(HoldGestureState.Idle, gesture.Release());
    }

    [Fact]
    public void ANegativeHoldTimeOrSlopIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HoldGesture(TimeSpan.FromMilliseconds(-1), 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HoldGesture(Hold, -1));
    }
}

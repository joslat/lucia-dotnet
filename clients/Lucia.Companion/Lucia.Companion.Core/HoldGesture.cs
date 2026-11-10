namespace Lucia.Companion.Core;

/// <summary>
/// Press and hold, then slide. A press becomes a hold after <c>holdTime</c> unless the pointer first moves further than
/// <c>slop</c>, which hands it back to scrolling or a tap. Time is supplied by the caller, so the rules can be tested without a clock.
/// </summary>
public sealed class HoldGesture
{
    private readonly TimeSpan holdTime;
    private readonly double slop;
    private double startX;
    private double startY;
    private TimeSpan pressedAt;

    public HoldGesture(TimeSpan holdTime, double slop)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(holdTime, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegative(slop);
        this.holdTime = holdTime;
        this.slop = slop;
    }

    public HoldGestureState State { get; private set; }

    public void Press(double x, double y, TimeSpan at)
    {
        (startX, startY, pressedAt) = (x, y, at);
        State = HoldGestureState.Pending;
    }

    /// <summary>Returns false when the movement cancelled a pending hold.</summary>
    public bool Move(double x, double y)
    {
        if (State != HoldGestureState.Pending) { return State == HoldGestureState.Holding; }
        if (Math.Sqrt(Math.Pow(x - startX, 2) + Math.Pow(y - startY, 2)) <= slop) { return true; }
        State = HoldGestureState.Idle;
        return false;
    }

    /// <summary>Returns true once, at the moment a pending press becomes a hold.</summary>
    public bool Tick(TimeSpan at)
    {
        if (State != HoldGestureState.Pending || at - pressedAt < holdTime) { return false; }
        State = HoldGestureState.Holding;
        return true;
    }

    /// <summary>Ends the gesture and reports the state it ended in.</summary>
    public HoldGestureState Release()
    {
        var ended = State;
        State = HoldGestureState.Idle;
        return ended;
    }
}

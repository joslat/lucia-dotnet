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
    /// <summary>A held pointer is beyond the press-origin tolerance. Returning to that origin cancels slide selection.</summary>
    public bool IsSliding { get; private set; }
    /// <summary>Elapsed hold charge, independent of animation frames. Cancel and release return it to zero.</summary>
    public double Progress(TimeSpan at) => State switch
    {
        HoldGestureState.Pending => holdTime == TimeSpan.Zero ? 1 : Math.Clamp((at - pressedAt).TotalMilliseconds / holdTime.TotalMilliseconds, 0, 1),
        HoldGestureState.Holding => 1,
        _ => 0,
    };

    public void Press(double x, double y, TimeSpan at)
    {
        (startX, startY, pressedAt) = (x, y, at);
        State = HoldGestureState.Pending;
        IsSliding = false;
    }

    /// <summary>Returns false when the movement cancelled a pending hold.</summary>
    public bool Move(double x, double y)
    {
        var distance = (x - startX) * (x - startX) + (y - startY) * (y - startY);
        if (State == HoldGestureState.Holding) { IsSliding = distance > slop * slop; return true; }
        if (State != HoldGestureState.Pending) { return false; }
        if (distance <= slop * slop) { return true; }
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
        IsSliding = false;
        return ended;
    }
}

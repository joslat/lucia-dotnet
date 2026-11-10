namespace Lucia.Companion.Core;

public enum HoldGestureState
{
    Idle,
    /// <summary>Pressed, waiting to see whether this becomes a hold.</summary>
    Pending,
    /// <summary>Held long enough without moving away; the pointer may now slide to an action.</summary>
    Holding,
}

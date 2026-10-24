namespace Lucia.Companion.Core;

/// <summary>Presentation state, not an inference that a room is safe or occupied.</summary>
public enum PlaceState
{
    Quiet,
    Changed,
    Working,
    Waiting,
    Unavailable,
    Problem
}

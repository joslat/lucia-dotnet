namespace Lucia.Companion.App;

/// <summary>
/// Whether the app may animate. The person can reduce motion in More; the app also pauses motion while it is in the
/// background. Every transition reads <see cref="IsEnabled"/> and is replaced by an immediate change when it is false.
/// Transitions retarget from their current value, so a new selection interrupts one in flight.
/// </summary>
public sealed class MotionSettings
{
    private bool reduceMotion;

    /// <summary>The app-wide instance. Held in memory for the process; it is not saved between launches yet.</summary>
    public static MotionSettings Current { get; } = new();

    public event Action? Changed;
    public bool IsPaused { get; private set; }
    public bool IsEnabled => !ReduceMotion && !IsPaused;

    public bool ReduceMotion
    {
        get => reduceMotion;
        set
        {
            if (reduceMotion == value) { return; }
            reduceMotion = value;
            Changed?.Invoke();
        }
    }

    public void Pause()
    {
        if (IsPaused) { return; }
        IsPaused = true;
        Changed?.Invoke();
    }

    public void Resume()
    {
        if (!IsPaused) { return; }
        IsPaused = false;
        Changed?.Invoke();
    }
}

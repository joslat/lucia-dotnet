using Lucia.Companion.App.Persistence;
using System.Text.Json;

namespace Lucia.Companion.App;

/// <summary>
/// Whether the app may animate. The person can reduce motion in More; the app also pauses motion while it is in the
/// background. Every transition reads <see cref="IsEnabled"/> and is replaced by an immediate change when it is false.
/// Transitions retarget from their current value, so a new selection interrupts one in flight.
/// </summary>
public sealed class MotionSettings
{
    private bool reduceMotion;
    private readonly LocalMotionPreferenceStore? store;

    public MotionSettings(LocalMotionPreferenceStore? store = null)
    {
        this.store = store;
        if (store is null) { return; }
        try
        {
            reduceMotion = store.Load();
            StorageStatus = "Motion preference is saved on this device.";
        }
        catch (Exception error) when (IsStorageError(error))
        {
            reduceMotion = true;
            HasStorageError = true;
            StorageStatus = "Motion preference couldn't be read. Motion is reduced for this session. Change the setting to save a new choice.";
        }
    }

    /// <summary>The app-wide instance. Platform launchers enable local storage before constructing the shared app.</summary>
    public static MotionSettings Current { get; private set; } = new();

    /// <summary>Call once at process startup, before controls subscribe to Current.</summary>
    public static void UseLocalPreferences() => Current = new(new LocalMotionPreferenceStore(LocalMotionPreferenceStore.DefaultPath));

    public bool HasStorageError { get; private set; }
    public string StorageStatus { get; private set; } = "Motion preference applies to this session.";

    public event Action? Changed;
    public bool IsPaused { get; private set; }
    public bool IsEnabled => !ReduceMotion && !IsPaused;

    public bool ReduceMotion
    {
        get => reduceMotion;
        set
        {
            if (reduceMotion == value && !HasStorageError) { return; }
            reduceMotion = value;
            if (store is not null)
            {
                try
                {
                    store.Save(value);
                    HasStorageError = false;
                    StorageStatus = "Motion preference is saved on this device.";
                }
                catch (Exception error) when (IsStorageError(error))
                {
                    HasStorageError = true;
                    StorageStatus = "Couldn't save Reduce motion. It applies to this session. Change the setting to retry.";
                }
            }
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

    private static bool IsStorageError(Exception error) => error is IOException or UnauthorizedAccessException or JsonException;
}

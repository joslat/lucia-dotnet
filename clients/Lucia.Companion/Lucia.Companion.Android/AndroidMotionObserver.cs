using Android.Content;
using Android.Database;
using Android.OS;
using Android.Provider;

namespace Lucia.Companion.Android;

/// <summary>Reads Android's animator policy on startup/resume and observes live changes on the main thread.</summary>
public sealed class AndroidMotionObserver(ContentResolver resolver) : ContentObserver(new Handler(Looper.MainLooper!))
{
    private bool observing;

    public static void ApplySystemPolicy(ContentResolver? resolver)
    {
        // The framework animator cache can lag this content-observer callback by one update.
        // Read the setting itself so enabling and disabling motion both apply to the current value.
        try
        {
            var scale = resolver is null ? 0 : Settings.Global.GetFloat(resolver, Settings.Global.AnimatorDurationScale, 1);
            App.MotionSettings.Current.SystemReduceMotion = !float.IsFinite(scale) || scale <= 0;
        }
        catch (Java.Lang.SecurityException) { App.MotionSettings.Current.SystemReduceMotion = true; }
    }

    public void Start()
    {
        if (!observing && Settings.Global.GetUriFor(Settings.Global.AnimatorDurationScale) is { } uri)
        {
            resolver.RegisterContentObserver(uri, false, this);
            observing = true;
        }
        ApplySystemPolicy(resolver);
    }

    public void Stop()
    {
        if (!observing) { return; }
        resolver.UnregisterContentObserver(this);
        observing = false;
    }

    public override void OnChange(bool selfChange) => ApplySystemPolicy(resolver);
}

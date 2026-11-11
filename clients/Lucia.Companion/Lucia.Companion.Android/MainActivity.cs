using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Android.Util;
using Avalonia.Android;

namespace Lucia.Companion.Android;

[Activity(Label = "Lucia prototype", Theme = "@style/LuciaTheme", MainLauncher = true,
    LaunchMode = LaunchMode.SingleTask,
    Exported = true, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode | ConfigChanges.FontScale)]
public sealed class MainActivity : AvaloniaMainActivity
{
    private AndroidMotionObserver? motionObserver;
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ApplyTextSizes();
        if (ContentResolver is { } resolver) { motionObserver = new AndroidMotionObserver(resolver); }
    }

    protected override void OnPause()
    {
        motionObserver?.Stop();
        (Avalonia.Application.Current as App.App)?.SuspendConnectionPolling();
        base.OnPause();
    }

    protected override void OnResume()
    {
        motionObserver?.Start();
        base.OnResume();
        (Avalonia.Application.Current as App.App)?.VerifyConnectionForeground();
    }

    protected override void OnDestroy()
    {
        motionObserver?.Stop();
        motionObserver?.Dispose();
        motionObserver = null;
        base.OnDestroy();
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        ApplyTextSizes();
    }

    private void ApplyTextSizes()
    {
        if (Resources?.DisplayMetrics is { Density: > 0 } metrics)
        {
            // Android's SP conversion applies its own font-scale curve, including API 34+ nonlinear sizing.
            App.CompanionTypography.Apply(size => TypedValue.ApplyDimension(ComplexUnitType.Sp, (float)size, metrics) / metrics.Density);
        }
    }
}

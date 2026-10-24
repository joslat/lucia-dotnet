using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace Lucia.Companion.Android;

[Activity(Label = "Lucia prototype", Theme = "@style/LuciaTheme", MainLauncher = true,
    Exported = true, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity;

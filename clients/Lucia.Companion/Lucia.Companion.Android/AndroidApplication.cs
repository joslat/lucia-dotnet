using Android.App;
using Android.Runtime;
using Avalonia.Android;

namespace Lucia.Companion.Android;

/// <summary>Avalonia 12 initializes the shared application at Android application lifetime, not activity creation.</summary>
[Application(AllowBackup = false)]
public sealed class AndroidApplication(nint javaReference, JniHandleOwnership transfer)
    : AvaloniaAndroidApplication<App.App>(javaReference, transfer);

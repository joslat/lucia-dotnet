using Android.App;
using Android.Runtime;
using Avalonia.Android;
using Lucia.Companion.Android.Security;
using Lucia.Companion.Connection;

namespace Lucia.Companion.Android;

/// <summary>Avalonia 12 initializes the shared application at Android application lifetime, not activity creation.</summary>
[Application(AllowBackup = false)]
public sealed class AndroidApplication(nint javaReference, JniHandleOwnership transfer)
    : AvaloniaAndroidApplication<App.App>(javaReference, transfer)
{
    public override void OnCreate()
    {
        App.App.ConnectionFactory = () => new CompanionConnection(new ProtectedSessionVault(
            Path.Combine(NoBackupFilesDir?.AbsolutePath ?? throw new IOException("Protected storage is unavailable."), "lucia-session"), new AndroidDeviceSecretProtector()));
        base.OnCreate();
    }
}

using Avalonia;
using Lucia.Companion.Connection;
using Lucia.Companion.Desktop.Security;

namespace Lucia.Companion.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (OperatingSystem.IsWindows()) { App.App.ConnectionFactory = CreateWindowsConnection; }
        AppBuilder.Configure<App.App>().UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime(args);
    }
    private static ICompanionConnection CreateWindowsConnection()
    {
        if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException(); }
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lucia", "Companion", "secure-session");
        return new CompanionConnection(new ProtectedSessionVault(directory, new WindowsDeviceSecretProtector()));
    }
}

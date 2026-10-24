using Avalonia;

namespace Lucia.Companion.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args) => AppBuilder.Configure<App.App>()
        .UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime(args);
}

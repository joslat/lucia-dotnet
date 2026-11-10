using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Lucia.Companion.Tests.TestAppBuilder))]

namespace Lucia.Companion.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App.App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
        // Headless frames don't advance animation clocks; reduced motion keeps captures deterministic. Motion tests opt in.
        .AfterSetup(_ => App.MotionSettings.Current.ReduceMotion = true);
}

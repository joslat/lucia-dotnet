using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Xunit;

namespace Lucia.Companion.Tests;

internal static class NativeCapture
{
    public static void Save(Window window, string filename)
    {
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var directory = Environment.GetEnvironmentVariable("LUCIA_COMPANION_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) { return; }
        Directory.CreateDirectory(directory);
        using var stream = File.Create(Path.Combine(directory, filename));
        frame.Save(stream, PngBitmapEncoderOptions.Default);
    }
}

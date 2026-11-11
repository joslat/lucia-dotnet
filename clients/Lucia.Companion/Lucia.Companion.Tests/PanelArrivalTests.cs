using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Media.Imaging;
using System.Diagnostics;
using System.Text.Json;
using Lucia.Companion.App;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class PanelArrivalTests
{
    [AvaloniaFact]
    public void PointerInputDuringArrivalStillChoosesTheRequestedNativeControl()
    {
        var originalTheme = Application.Current!.RequestedThemeVariant;
        MotionSettings.Current.ReduceMotion = false;
        var more = new MoreScreen();
        var window = new Window { Width = 390, Height = 844, Content = more };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            Assert.Contains(more.GetVisualDescendants().OfType<Border>(), panel => panel.Classes.Contains("panel") && panel.Opacity < 1);
            var dark = more.FindControl<RadioButton>("ThemeDark")!;
            var point = dark.TranslatePoint(new Point(dark.Bounds.Width / 2, dark.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
            Assert.True(dark.IsChecked);
            Assert.Equal(ThemeVariant.Dark, Application.Current.RequestedThemeVariant);
        }
        finally { Application.Current.RequestedThemeVariant = originalTheme; MotionSettings.Current.ReduceMotion = true; window.Close(); }
    }

    [AvaloniaFact]
    public async Task APanelMovedDuringResponsiveReflowSettlesAndCannotReplayAnOldDelay()
    {
        MotionSettings.Current.ReduceMotion = false;
        var home = new HomeScreen(new HomeViewModel(SampleHome.Create()));
        var window = new Window { Width = 1194, Height = 844, Content = home };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var attention = home.FindControl<Border>("AttentionCard")!;
            Assert.True(attention.Opacity < 1);
            window.Width = 390; Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, attention.Opacity);
            await Task.Delay(120, TestContext.Current.CancellationToken);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, attention.Opacity);
        }
        finally { MotionSettings.Current.ReduceMotion = true; window.Close(); }
    }

    [AvaloniaFact]
    public async Task RealScreenPanelsFadeAndRiseIntoPlaceWhileInputAndFocusRemainAvailable()
    {
        MotionSettings.Current.ReduceMotion = false;
        var more = new MoreScreen();
        var window = new Window { Width = 390, Height = 844, Content = more };
        var samples = new List<(double Opacity, double Y)>();
        var captured = new List<(Bitmap Frame, double At, double Finished, double Opacity, double Y)>();
        var captureDirectory = Environment.GetEnvironmentVariable("LUCIA_COMPANION_CAPTURE_DIR");
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var panels = more.GetVisualDescendants().OfType<Border>().Where(control => control.Classes.Contains("panel")).ToArray();
            Assert.True(panels.Length >= 3);
            var theme = more.FindControl<RadioButton>("ThemeSystem")!;
            Assert.True(theme.Focus());
            var clock = Stopwatch.StartNew();
            for (var frame = 0; frame < 18; frame++)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
                samples.Add((panels[0].Opacity, panels[0].RenderTransform?.Value.M32 ?? 0));
                Assert.True(theme.IsEffectivelyVisible && theme.IsEffectivelyEnabled && theme.IsHitTestVisible);
                Assert.Same(theme, window.FocusManager!.GetFocusedElement());
                if (!string.IsNullOrWhiteSpace(captureDirectory))
                {
                    var at = clock.Elapsed.TotalMilliseconds;
                    var bitmap = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Native frame unavailable.");
                    captured.Add((bitmap, at, clock.Elapsed.TotalMilliseconds, samples[^1].Opacity, samples[^1].Y));
                }
                await Task.Delay(20, TestContext.Current.CancellationToken);
            }
            Assert.Contains(samples, frame => frame.Opacity > 0 && frame.Opacity < .99 && frame.Y > 0);
            Assert.All(panels, panel => { Assert.Equal(1, panel.Opacity); Assert.Equal(0, panel.RenderTransform?.Value.M32 ?? 0, 3); });
        }
        finally
        {
            MotionSettings.Current.ReduceMotion = true; window.Close();
            if (!string.IsNullOrWhiteSpace(captureDirectory))
            {
                Directory.CreateDirectory(captureDirectory);
                for (var index = 0; index < captured.Count; index++)
                {
                    using var frame = captured[index].Frame;
                    using var output = File.Create(Path.Combine(captureDirectory, $"panel-arrival-{index:00}.png"));
                    frame.Save(output, PngBitmapEncoderOptions.Default);
                }
                File.WriteAllText(Path.Combine(captureDirectory, "panel-arrival.json"), JsonSerializer.Serialize(new
                {
                    scope = "Actual native headless frames; timestamps measured; PNG encoding after interaction; no hardware pacing claim",
                    frames = captured.Select((frame, index) => new { file = $"panel-arrival-{index:00}.png", frame.At, frame.Finished, frame.Opacity, frame.Y }),
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
    }

    [AvaloniaFact]
    public async Task ReducingMotionOrLeavingTheScreenSettlesPendingPanelsWithoutLaterReplay()
    {
        MotionSettings.Current.ReduceMotion = false;
        var more = new MoreScreen();
        var parent = new Border { Child = more };
        var window = new Window { Width = 390, Height = 844, Content = parent };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var panels = more.GetVisualDescendants().OfType<Border>().Where(control => control.Classes.Contains("panel")).ToArray();
            Assert.Contains(panels, panel => panel.Opacity < 1);
            MotionSettings.Current.SystemReduceMotion = true;
            Assert.All(panels, panel => Assert.Equal(1, panel.Opacity));
            MotionSettings.Current.SystemReduceMotion = false;
            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.All(panels, panel => Assert.Equal(1, panel.Opacity));
            parent.IsVisible = false;
            parent.IsVisible = true;
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(panels, panel => panel.Opacity < 1);
            parent.IsVisible = false;
            Assert.All(panels, panel => Assert.Equal(1, panel.Opacity));
            await Task.Delay(350, TestContext.Current.CancellationToken);
            Assert.All(panels, panel => Assert.Equal(1, panel.Opacity));
        }
        finally { MotionSettings.Current.ReduceMotion = true; MotionSettings.Current.SystemReduceMotion = false; window.Close(); }
    }
}

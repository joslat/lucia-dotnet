using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

/// <summary>Real intermediate native renders with measured timestamps, not a hardware frame-pacing benchmark.</summary>
public sealed class MotionSequenceQualificationTests
{
    private readonly List<(Bitmap Frame, string Path)> pendingFrames = [];
    [AvaloniaFact]
    public async Task BloomReversesFromItsPaintedStateAndOnlyTheFinalFoldDismisses()
    {
        var bloom = new BloomActions();
        bloom.SetPlace(SampleHome.Create().Places[0], false);
        var window = new Window { Width = 390, Height = 400, Content = bloom };
        var dismissed = false;
        try
        {
            window.Show();
            bloom.PlayOpening(true);
            var complete = await Capture(window, "bloom-unfold", 350, () => BloomState(bloom));
            Assert.Contains(complete, frame => frame["opacity"] > 0 && frame["opacity"] < 1);
            Assert.Equal(1, complete[^1]["opacity"], 3);
            bloom.PlayOpening(true);
            var open = await Capture(window, "bloom-open", 90, () => BloomState(bloom));
            Assert.Contains(open, frame => frame["opacity"] > 0 && frame["opacity"] < 1);
            var beforeFold = bloom.Opacity;
            bloom.PlayClosing(true, () => dismissed = true);
            Assert.InRange(Math.Abs(beforeFold - bloom.Opacity), 0, .01);
            await Capture(window, "bloom-interrupted-fold", 60, () => BloomState(bloom));
            var beforeReopen = bloom.Opacity;
            bloom.PlayOpening(true);
            Assert.InRange(Math.Abs(beforeReopen - bloom.Opacity), 0, .01);
            await Capture(window, "bloom-reopen", 330, () => BloomState(bloom));
            Assert.False(dismissed);
            Assert.Equal(1, bloom.Opacity, 3);
            Assert.Equal(1, bloom.RenderTransform?.Value.M11 ?? 1, 3);
            bloom.PlayClosing(true, () => dismissed = true);
            var closing = await Capture(window, "bloom-final-fold", 260, () => BloomState(bloom));
            Assert.Contains(closing, frame => frame["opacity"] > 0 && frame["opacity"] < 1);
            Assert.True(dismissed);
            bloom.PlayOpening(false);
            await Capture(window, "bloom-reduced", 50, () => BloomState(bloom));
            Assert.Equal(1, bloom.Opacity);
            Assert.Null(bloom.Transitions);
        }
        finally { window.Close(); SaveFrames(); }
    }

    [AvaloniaFact]
    public async Task ArrivalDeceleratesWithoutHidingTextAndSystemReductionSettlesItImmediately()
    {
        var panel = new Border { Child = new TextBlock { Text = "Verified reports are ready", Margin = new Thickness(20) } };
        SurfaceEntrance.SetEnabled(panel, true);
        MotionSettings.Current.ReduceMotion = false;
        var window = new Window { Width = 390, Height = 120, Content = panel };
        Dictionary<string, double> State() => new() { ["offsetY"] = panel.RenderTransform?.Value.M32 ?? 0, ["opacity"] = panel.Opacity };
        try
        {
            window.Show();
            var frames = await Capture(window, "arrival", 320, State);
            Assert.Contains(frames, frame => frame["offsetY"] > 0 && frame["offsetY"] < 4);
            Assert.All(frames, frame => Assert.Equal(1, frame["opacity"]));
            Assert.Equal(0, frames[^1]["offsetY"], 3);
            panel.IsVisible = false;
            panel.IsVisible = true;
            await Capture(window, "arrival-before-reduction", 65, State);
            MotionSettings.Current.SystemReduceMotion = true;
            var reduced = await Capture(window, "arrival-reduced", 80, State);
            Assert.All(reduced, frame => Assert.Equal(0, frame["offsetY"]));
            Assert.Null(panel.Transitions);
        }
        finally { MotionSettings.Current.ReduceMotion = true; MotionSettings.Current.SystemReduceMotion = false; window.Close(); SaveFrames(); }
    }

    [AvaloniaFact]
    public async Task ExactValuesStayVisibleWhileFiniteEmphasisDecaysAndReductionCancelsIt()
    {
        var at = new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);
        Observation Reading(string value) => new("Temperature", value, ObservationQuality.Current, at) { Role = ObservationRole.Temperature };
        var row = new ObservationRow(Reading("21.4 °C"));
        MotionSettings.Current.ReduceMotion = false;
        var window = new Window { Width = 390, Height = 160, Content = row };
        Dictionary<string, double> State() => new() { ["emphasis"] = row.EmphasisOpacity };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            row.Update(Reading("22.9 °C"));
            Assert.Equal("22.9 °C", row.FindControl<TextBlock>("ValueText")!.Text);
            var frames = await Capture(window, "value-change", 450, State);
            Assert.Contains(frames, frame => frame["emphasis"] > 0 && frame["emphasis"] < .7);
            Assert.Equal(0, frames[^1]["emphasis"], 3);
            row.Update(Reading("23.1 °C"));
            await Capture(window, "value-before-reduction", 60, State);
            MotionSettings.Current.ReduceMotion = true;
            var reduced = await Capture(window, "value-reduced", 65, State);
            Assert.All(reduced, frame => Assert.Equal(0, frame["emphasis"]));
            Assert.Equal("23.1 °C", row.FindControl<TextBlock>("ValueText")!.Text);
        }
        finally { MotionSettings.Current.ReduceMotion = true; window.Close(); SaveFrames(); }
    }

    [AvaloniaFact]
    public async Task TheRealHoldChargesBeforeOpeningAndReleaseClearsItsCharge()
    {
        var constellation = new HomeConstellation();
        constellation.SetHome(SampleHome.Create());
        var window = new Window { Width = 390, Height = 560, Content = constellation };
        MotionSettings.Current.ReduceMotion = false;
        var opened = false;
        constellation.HoldStarted += _ => opened = true;
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var orb = constellation.Orbs.First();
            var target = orb.GetVisualDescendants().OfType<Control>().First(item => item.Name == "Orb");
            var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            var frames = await Capture(window, "hold-charge", 560, () => new() { ["charge"] = orb.HoldProgress, ["opened"] = opened ? 1 : 0 });
            Assert.Contains(frames, frame => frame["charge"] > 0 && frame["charge"] < 1 && frame["opened"] == 0);
            Assert.True(opened);
            Assert.True(constellation.IsHolding);
            window.MouseUp(point, MouseButton.Left);
            Assert.Equal(0, orb.HoldProgress);
            Assert.False(constellation.IsHolding);
        }
        finally { MotionSettings.Current.ReduceMotion = true; window.Close(); SaveFrames(); }
    }

    private static Dictionary<string, double> BloomState(BloomActions bloom)
    {
        var petal = bloom.FindControl<Grid>("OpenPetal")!;
        return new() { ["opacity"] = bloom.Opacity, ["scale"] = bloom.RenderTransform?.Value.M11 ?? 1,
            ["petalOpacity"] = petal.Opacity, ["petalX"] = petal.RenderTransform?.Value.M31 ?? 0,
            ["petalY"] = petal.RenderTransform?.Value.M32 ?? 0 };
    }

    private async Task<List<Dictionary<string, double>>> Capture(Window window, string name, int milliseconds, Func<Dictionary<string, double>> state)
    {
        var directory = Environment.GetEnvironmentVariable("LUCIA_COMPANION_CAPTURE_DIR");
        if (!string.IsNullOrWhiteSpace(directory)) { Directory.CreateDirectory(Path.Combine(directory, "motion")); }
        var frames = new List<Dictionary<string, double>>();
        var clock = Stopwatch.StartNew();
        do
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
            var frame = state();
            frame["sampleMilliseconds"] = clock.Elapsed.TotalMilliseconds;
            if (!string.IsNullOrWhiteSpace(directory))
            {
                pendingFrames.Add((window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Native render unavailable."),
                    Path.Combine(directory, "motion", $"{name}-{frames.Count:000}.png")));
            }
            frame["captureFinishedMilliseconds"] = clock.Elapsed.TotalMilliseconds;
            frames.Add(frame);
            if (clock.Elapsed.TotalMilliseconds >= milliseconds) { break; }
            await Task.Delay(10, TestContext.Current.CancellationToken);
        } while (true);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            File.WriteAllText(Path.Combine(directory, "motion", name + ".json"), JsonSerializer.Serialize(new
            { name, scope = "Native headless software renders; timestamps measured, no hardware/vsync claim", frames }, new JsonSerializerOptions { WriteIndented = true }));
        }
        return frames;
    }

    private void SaveFrames()
    {
        // Encoding after the complete interaction keeps PNG compression out of reversal and interruption timing.
        foreach (var (frame, path) in pendingFrames)
        {
            using (frame)
            using (var output = File.Create(path)) { frame.Save(output, PngBitmapEncoderOptions.Default); }
        }
        pendingFrames.Clear();
    }
}

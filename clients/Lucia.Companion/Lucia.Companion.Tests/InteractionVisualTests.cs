using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class InteractionVisualTests
{
    [AvaloniaTheory]
    [InlineData(390, 844, false)]
    [InlineData(390, 844, true)]
    [InlineData(1194, 834, false)]
    [InlineData(1194, 834, true)]
    public void PointerHoverBloomAndPressRetainTheNativeLayouts(int width, int height, bool dark)
    {
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = width, Height = height, Content = shell };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            var constellation = home.FindControl<HomeConstellation>("Constellation")!;
            var porch = constellation.Orbs.Single(orb => (string)orb.Tag! == "porch");
            window.MouseMove(Center(porch, window));
            Assert.Contains("mouse-over", porch.Classes);
            NativeCapture.Save(window, $"interaction-hover-{width}-{(dark ? "dark" : "light")}.png");
            state.OpenActionsFor("porch"); Dispatcher.UIThread.RunJobs();
            var bloom = home.FindControl<BloomActions>("PlaceActions")!;
            var pin = bloom.FindControl<Button>("PinButton")!;
            window.MouseMove(Center(pin, window));
            NativeCapture.Save(window, $"interaction-bloom-{width}-{(dark ? "dark" : "light")}.png");
            window.MouseDown(Center(pin, window), MouseButton.Left);
            Assert.True(pin.IsPressed);
            NativeCapture.Save(window, $"interaction-press-{width}-{(dark ? "dark" : "light")}.png");
            window.MouseUp(Center(pin, window), MouseButton.Left);
            Assert.True(state.IsPinned("porch"));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task BloomProducesIntermediateRenderedFramesAndSettles()
    {
        MotionSettings.Current.ReduceMotion = false;
        var bloom = new BloomActions();
        bloom.SetPlace(SampleHome.Create().Places[0], pinned: false);
        var window = new Window { Width = 390, Height = 400, Content = bloom };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            bloom.PlayOpening(true); Dispatcher.UIThread.RunJobs();
            var samples = new List<double>();
            for (var frame = 0; frame < 12; frame++)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
                samples.Add(bloom.Opacity);
                if (frame is 0 or 2 or 5 or 11) { NativeCapture.Save(window, $"interaction-unfold-frame-{frame}.png"); }
                await Task.Delay(30, TestContext.Current.CancellationToken);
            }
            Assert.Contains(samples, value => value > 0 && value < 1);
            Assert.Equal(1, samples[^1], precision: 2);
        }
        finally { MotionSettings.Current.ReduceMotion = true; window.Close(); }
    }

    private static Point Center(Control control, Window window)
    {
        var target = control is PlaceOrb ? control.GetVisualDescendants().OfType<Control>().First(item => item.Name == "Orb") : control;
        return target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
    }
}

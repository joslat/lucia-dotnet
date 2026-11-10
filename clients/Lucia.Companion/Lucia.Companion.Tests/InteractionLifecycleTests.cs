using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class InteractionLifecycleTests
{
    [AvaloniaFact]
    public async Task ScrollingOrLosingTheControlClearsItsChargeAndStopsThePendingHold()
    {
        var motion = MotionSettings.Current;
        motion.ReduceMotion = false;
        var home = new HomeScreen(new HomeViewModel(SampleHome.Create()));
        var window = new Window { Width = 1194, Height = 834, Content = home };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var constellation = home.FindControl<HomeConstellation>("Constellation")!;
            constellation.HoldDelay = TimeSpan.FromSeconds(1);
            var orb = constellation.Orbs.First();
            var center = Center(orb, window);
            window.MouseMove(center);
            window.MouseDown(center, MouseButton.Left);
            await Task.Delay(80, TestContext.Current.CancellationToken);
            Assert.InRange(orb.HoldProgress, .001, .99);
            NativeCapture.Save(window, "interaction-charge-progress.png");
            window.MouseMove(center + new Vector(40, 0));
            Assert.Equal(0, orb.HoldProgress);
            Assert.False(constellation.IsHolding);
            window.MouseUp(center + new Vector(40, 0), MouseButton.Left);
            motion.ReduceMotion = true;
            window.MouseDown(center, MouseButton.Left);
            Assert.Equal(1, orb.HoldProgress);
            window.Close();
            Assert.Equal(0, orb.HoldProgress);
            Assert.False(constellation.IsHolding);
        }
        finally { motion.ReduceMotion = true; motion.Resume(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task HoveringAnActionProtectsTheChoiceAndBackgroundingClosesTheMenu()
    {
        var bloom = new BloomActions { IdleDelay = TimeSpan.FromMilliseconds(20) };
        var window = new Window { Width = 400, Height = 400, Content = bloom };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var closes = 0;
            bloom.ActionRequested += action => { if (action == BloomAction.Close) { closes++; } };
            bloom.BeginSession(pointerInitiated: true, holdActive: false);
            var pin = bloom.FindControl<Button>("PinButton")!;
            window.MouseMove(Center(pin, window));
            await Task.Delay(70, TestContext.Current.CancellationToken);
            Assert.Equal(0, closes);
            MotionSettings.Current.Pause();
            Assert.Equal(1, closes);
        }
        finally { MotionSettings.Current.Resume(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task NavigationHelpAppearsAfterTheDelayAndCanBeReadWithThePointer()
    {
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = 1194, Height = 834, Content = shell };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var more = shell.Navigation.ButtonFor(ShellDestination.More);
            window.MouseMove(Center(more, window));
            Assert.False(ToolTip.GetIsOpen(more));
            await Task.Delay(780, TestContext.Current.CancellationToken);
            Assert.True(ToolTip.GetIsOpen(more));
            NativeCapture.Save(window, "interaction-context-help.png");
        }
        finally { window.Close(); }
    }

    private static Point Center(Control control, Window window) => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
}

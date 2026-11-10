using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

/// <summary>The test app starts with motion reduced so captures are deterministic; these tests turn it on deliberately.</summary>
public sealed class MotionTests
{
    [Fact]
    public void MotionRunsOnlyWhenNotReducedAndNotInTheBackground()
    {
        var motion = new MotionSettings();
        var changes = 0;
        motion.Changed += () => changes++;
        Assert.True(motion.IsEnabled);
        motion.ReduceMotion = true;
        Assert.False(motion.IsEnabled);
        motion.ReduceMotion = false;
        motion.Pause();
        Assert.False(motion.IsEnabled);
        Assert.True(motion.IsPaused);
        motion.Resume();
        Assert.True(motion.IsEnabled);
        Assert.Equal(4, changes);
        motion.Resume();
        Assert.Equal(4, changes);
    }

    [AvaloniaFact]
    public void DestinationsCrossFadeAndReducedMotionTurnsItOff()
    {
        var motion = MotionSettings.Current;
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show();
            var host = Assert.IsType<TransitioningContentControl>(shell.FindControl<ContentControl>("ScreenHost"));
            Assert.Null(host.PageTransition);
            motion.ReduceMotion = false;
            var fade = Assert.IsType<CrossFade>(host.PageTransition);
            Assert.Equal(TimeSpan.FromMilliseconds(180), fade.Duration);
            motion.Pause();
            Assert.Null(host.PageTransition);
            motion.Resume();
            Assert.NotNull(host.PageTransition);
        }
        finally { motion.ReduceMotion = true; motion.Resume(); window.Close(); }
    }

    [AvaloniaFact]
    public void MoreOffersReduceMotion()
    {
        var motion = MotionSettings.Current;
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show();
            var more = ShellNavigationTests.OpenMore(shell);
            var toggle = more.FindControl<CheckBox>("ReduceMotion")!;
            Assert.True(toggle.IsChecked);
            toggle.IsChecked = false;
            Assert.False(motion.ReduceMotion);
            toggle.IsChecked = true;
            Assert.True(motion.ReduceMotion);
        }
        finally { motion.ReduceMotion = true; window.Close(); }
    }

    [AvaloniaFact]
    public void BloomBloomsOpenOnlyWithMotion()
    {
        var motion = MotionSettings.Current;
        var state = new HomeViewModel(SampleHome.Create());
        var home = new HomeScreen(state);
        var window = new Window { Width = 390, Height = 844, Content = home };
        try
        {
            window.Show();
            var bloom = home.FindControl<BloomActions>("PlaceActions")!;
            state.SelectPlace("porch"); state.CloseDetail(); state.OpenActions();
            Dispatcher.UIThread.RunJobs();
            Assert.Null(bloom.Transitions);
            Assert.Equal(1, bloom.Opacity);
            state.CloseActions();
            Dispatcher.UIThread.RunJobs();

            motion.ReduceMotion = false;
            state.OpenActions();
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(bloom.Transitions);
            Assert.Contains(bloom.Transitions!, transition => transition is DoubleTransition { Property.Name: "Opacity", Duration.TotalMilliseconds: 200 });
        }
        finally { motion.ReduceMotion = true; window.Close(); }
    }

    [AvaloniaFact]
    public void TheStoryHandAnimatesOnlyWithMotionAndItsOwnSwitch()
    {
        var motion = MotionSettings.Current;
        var view = new StoryScreen(SampleStory.Navigator(SampleHome.Create()));
        var window = new Window { Width = 390, Height = 844, Content = view };
        try
        {
            window.Show();
            var orbit = view.FindControl<StoryOrbit>("Orbit")!;
            Assert.True(view.FindControl<CheckBox>("AnimateSelection")!.IsChecked);
            Assert.Null(orbit.Transitions);
            motion.ReduceMotion = false;
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(orbit.Transitions);
            view.FindControl<CheckBox>("AnimateSelection")!.IsChecked = false;
            Assert.Null(orbit.Transitions);
        }
        finally { motion.ReduceMotion = true; window.Close(); }
    }

    [AvaloniaFact]
    public void BackgroundingTheAppPausesMotion()
    {
        var app = Assert.IsType<Lucia.Companion.App.App>(Application.Current);
        app.SuspendConnectionPolling();
        Assert.True(MotionSettings.Current.IsPaused);
        app.VerifyConnectionForeground();
        Assert.False(MotionSettings.Current.IsPaused);
    }
}

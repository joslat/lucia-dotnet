using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Headless;
using Avalonia.Controls.Presenters;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

/// <summary>The test app starts with motion reduced so captures are deterministic; these tests turn it on deliberately.</summary>
public sealed class MotionTests
{
    [AvaloniaFact]
    public async Task ReenablingMotionAndNavigatingDoesNotLetAnOldCancellationHideTheNewPage()
    {
        var motion = MotionSettings.Current;
        motion.ReduceMotion = false;
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            shell.Navigate(ShellDestination.More); Dispatcher.UIThread.RunJobs();
            await Task.Delay(35, TestContext.Current.CancellationToken);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
            motion.SystemReduceMotion = true;
            motion.SystemReduceMotion = false;
            shell.Navigate(ShellDestination.Story);
            for (var index = 0; index < 8; index++)
            {
                await Task.Delay(35, TestContext.Current.CancellationToken);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
            }
            var story = Assert.IsType<StoryScreen>(shell.CurrentScreen);
            Assert.True(story.IsEffectivelyVisible);
            Assert.All(story.GetVisualAncestors().TakeWhile(item => !ReferenceEquals(item, shell)), item => Assert.Equal(1, item.Opacity));
        }
        finally { motion.ReduceMotion = true; motion.SystemReduceMotion = false; window.Close(); }
    }

    [AvaloniaFact]
    public async Task ASystemMotionRequestSettlesANavigationFadeAlreadyInFlight()
    {
        var motion = MotionSettings.Current;
        motion.ReduceMotion = false;
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            shell.Navigate(ShellDestination.More); Dispatcher.UIThread.RunJobs();
            var screen = Assert.IsType<MoreScreen>(shell.CurrentScreen);
            var presenter = screen.GetVisualAncestors().OfType<ContentPresenter>().First();
            for (var index = 0; index < 8 && (presenter.Opacity <= 0 || presenter.Opacity >= 1); index++)
            {
                await Task.Delay(15, TestContext.Current.CancellationToken);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
            }
            Assert.InRange(presenter.Opacity, .001, .999);
            motion.SystemReduceMotion = true;
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, presenter.Opacity);
            Assert.False(screen.FindControl<CheckBox>("ReduceMotion")!.IsChecked);
            Assert.True(screen.FindControl<TextBlock>("SystemMotionStatus")!.IsVisible);
            Assert.Null(shell.FindControl<TransitioningContentControl>("ScreenHost")!.PageTransition);
        }
        finally { motion.ReduceMotion = true; motion.SystemReduceMotion = false; window.Close(); }
    }

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
            var fade = Assert.IsType<CompanionCrossFade>(host.PageTransition);
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
            var tracks = view.GetVisualDescendants().OfType<Control>().Where(item => item.GetType().Name == "TimelineTrackLayer").ToArray();
            Assert.NotEmpty(tracks);
            Assert.All(tracks, track => Assert.Contains(track.Transitions!, transition => transition is DoubleTransition { Property.Name: "CursorFraction", Duration.TotalMilliseconds: 280 }));
            view.FindControl<CheckBox>("AnimateSelection")!.IsChecked = false;
            Assert.Null(orbit.Transitions);
            Assert.All(tracks, track => Assert.Null(track.Transitions));
        }
        finally { motion.ReduceMotion = true; window.Close(); }
    }

    [AvaloniaFact]
    public void BackgroundingTheAppPausesMotion()
    {
        var app = Assert.IsType<Lucia.Companion.App.App>(Application.Current);
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Content = shell };
        var motion = MotionSettings.Current;
        try
        {
            motion.ReduceMotion = false;
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var button = shell.Navigation.ButtonFor(ShellDestination.More);
            Assert.NotNull(button.Transitions);
            app.SuspendConnectionPolling();
            Assert.True(motion.IsPaused);
            Assert.Null(button.Transitions);
            app.VerifyConnectionForeground();
            Assert.False(motion.IsPaused);
            Assert.NotNull(button.Transitions);
        }
        finally { motion.ReduceMotion = true; motion.Resume(); window.Close(); }
    }
}

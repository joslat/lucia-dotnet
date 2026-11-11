using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

public sealed class StoryInteractionContinuityTests
{
    [AvaloniaFact]
    public void ChaptersAfterConsecutiveDateChangesUseTheVisibleDaysWordsAndSelection()
    {
        var screen = new StoryScreen(SampleStory.Navigator(SampleHome.Create()));
        var window = new Window { Width = 1194, Height = 834, Content = screen };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var previous = screen.FindControl<Button>("PreviousDate")!;
            previous.Focus(); Activate(window); Activate(window);
            Assert.Equal(new DateOnly(2026, 10, 22), screen.State.Day.Date);
            var expected = screen.State.VisibleChapters.Last();
            var button = screen.FindControl<ChapterList>("Chapters")!.GetVisualDescendants().OfType<Button>().Single(item => Equals(item.Tag, expected.Id));
            Assert.Contains(screen.State.Day.TimeLabel(expected.Start)[..5], AutomationProperties.GetName(button));
            button.BringIntoView(); button.Focus(); Activate(window);
            Assert.Equal(expected.Id, screen.State.SelectedChapterId);
            Assert.Equal(expected.Start, screen.State.SelectedInstant);
            Assert.Equal(screen.State.CursorFraction, screen.FindControl<Slider>("TimeScrubber")!.Value);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SelectingAReportKeepsItsButtonAndKeyboardFocusWhenItFiltersTheStory()
    {
        var screen = new StoryScreen();
        var window = new Window { Width = 1194, Height = 834, Content = screen };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var report = screen.GetVisualDescendants().OfType<PlaceTimeline>().SelectMany(item => item.GetVisualDescendants().OfType<Button>()).First();
            var id = (string)report.Tag!;
            var expected = screen.State.Day.Intervals.Single(item => item.Id == id);
            report.BringIntoView(); report.Focus(); Activate(window);
            Assert.Equal(expected.PlaceId, screen.State.SelectedPlaceId);
            Assert.Equal(expected.Start, screen.State.SelectedInstant);
            Assert.Same(report, window.FocusManager!.GetFocusedElement());
            Assert.True(report.IsEffectivelyVisible);
            Assert.NotNull(report.Parent);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void BloomKeepsTheChosenActionFocusedAcrossLiveTextSizeChanges()
    {
        var home = new HomeScreen(new HomeViewModel(SampleHome.Create()));
        var window = new Window { Width = 390, Height = 844, Content = home };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            home.State.OpenActionsFor("garden"); Dispatcher.UIThread.RunJobs();
            var bloom = home.FindControl<BloomActions>("PlaceActions")!;
            var radial = bloom.FindControl<Button>("PinButton")!;
            Assert.True(radial.Focus());
            Assert.True(radial.IsFocused);
            Assert.Same(radial, TopLevel.GetTopLevel(bloom)!.FocusManager!.GetFocusedElement());
            CompanionTypography.Apply(size => size * 2); Dispatcher.UIThread.RunJobs();
            var large = bloom.FindControl<Button>("LargePin")!;
            Assert.Same(large, TopLevel.GetTopLevel(bloom)!.FocusManager!.GetFocusedElement());
            Assert.True(large.IsEffectivelyVisible);
            CompanionTypography.Apply(size => size); Dispatcher.UIThread.RunJobs();
            Assert.Same(bloom.FindControl<Button>("PinButton"), TopLevel.GetTopLevel(bloom)!.FocusManager!.GetFocusedElement());
        }
        finally { CompanionTypography.Apply(size => size); window.Close(); }
    }

    [AvaloniaFact]
    public async Task HidingHomeCancelsAPendingHoldBeforeItCanOpenAnOverlay()
    {
        MotionSettings.Current.ReduceMotion = false;
        var home = new HomeScreen(new HomeViewModel(SampleHome.Create()));
        var parent = new Border { Child = home };
        var window = new Window { Width = 1194, Height = 834, Content = parent };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var constellation = home.FindControl<HomeConstellation>("Constellation")!;
            constellation.HoldDelay = TimeSpan.FromMilliseconds(100);
            var opened = 0;
            constellation.HoldStarted += _ => opened++;
            var orb = constellation.Orbs.First();
            var center = orb.TranslatePoint(new Point(orb.Bounds.Width / 2, orb.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center); window.MouseDown(center, MouseButton.Left);
            parent.IsVisible = false;
            await Task.Delay(180, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, opened);
            Assert.Equal(0, orb.HoldProgress);
            Assert.False(constellation.IsHolding);
            Assert.False(home.FindControl<Popup>("ActionPopup")!.IsOpen);
        }
        finally { MotionSettings.Current.ReduceMotion = true; window.Close(); }
    }

    private static void Activate(Window window)
    {
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();
    }
}

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

public sealed class StoryEntryContextTests
{
    [AvaloniaTheory]
    [InlineData(390, false, false)]
    [InlineData(390, true, true)]
    [InlineData(1194, false, true)]
    [InlineData(1194, true, false)]
    public void BloomHistoryRevealsTodayAndItsLatePlaceAfterADeepStoryVisit(int width, bool large, bool dark)
    {
        var previousTheme = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        CompanionTypography.Apply(size => large ? size * 2 : size);
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = width, Height = 844, Content = shell };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            ShellNavigationTests.Navigate(shell, ShellDestination.Story);
            var story = Assert.IsType<StoryScreen>(shell.CurrentScreen);
            story.FindControl<Button>("PreviousDate")!.Focus(); Activate(window);
            Assert.NotEqual(SampleStory.Today, story.State.Day.Date);
            story.FindControl<CheckBox>("AnimateSelection")!.BringIntoView(); Dispatcher.UIThread.RunJobs();
            Assert.True(story.FindControl<ScrollViewer>("StoryScroll")!.Offset.Y > 100);
            ShellNavigationTests.Navigate(shell, ShellDestination.Home);
            var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            state.OpenActionsFor("utility"); Dispatcher.UIThread.RunJobs();
            var history = home.FindControl<BloomActions>("PlaceActions")!.FindControl<Button>(large ? "LargeHistory" : "HistoryButton")!;
            Assert.True(history.Focus()); Activate(window);
            Assert.Same(story, shell.CurrentScreen);
            Assert.Equal(SampleStory.Today, story.State.Day.Date); Assert.Equal("utility", story.State.SelectedPlaceId);
            NativeCapture.Save(window, $"history-entry-{width}-{large}-{dark}.png");
            AssertInside(story.FindControl<TextBlock>("DateLabel")!, window);
            AssertInside(Filter(story, "utility"), window);
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); Application.Current.RequestedThemeVariant = previousTheme; }
    }

    [AvaloniaTheory]
    [InlineData(390, false)]
    [InlineData(390, true)]
    public void OrdinaryFilterChangesRevealTheirChipWithoutMovingVerticalReadingOrFocus(int width, bool large)
    {
        CompanionTypography.Apply(size => large ? size * 2 : size);
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = width, Height = 844, Content = shell };
        try
        {
            window.Show(); ShellNavigationTests.Navigate(shell, ShellDestination.Story);
            var story = Assert.IsType<StoryScreen>(shell.CurrentScreen);
            var scroll = story.FindControl<ScrollViewer>("StoryScroll")!;
            scroll.Offset = new Vector(0, 200); Dispatcher.UIThread.RunJobs();
            Assert.Equal(200, scroll.Offset.Y);
            var focus = shell.Navigation.ButtonFor(ShellDestination.Story); Assert.True(focus.Focus());
            story.State.SelectPlace("utility"); Dispatcher.UIThread.RunJobs();
            Assert.Equal(200, scroll.Offset.Y); Assert.Same(focus, window.FocusManager!.GetFocusedElement());
            var chip = Filter(story, "utility");
            var strip = chip.GetVisualAncestors().OfType<ScrollViewer>().First();
            var point = chip.TranslatePoint(default, strip)!.Value;
            Assert.True(point.X >= -.1 && point.X + chip.Bounds.Width <= strip.Viewport.Width + .1,
                $"Active Utility filter at {point.X} with width {chip.Bounds.Width}, strip viewport {strip.Viewport.Width}.");
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    [AvaloniaTheory]
    [InlineData(390)]
    [InlineData(1194)]
    public void WholeHomeStoryRevealsTodayAndAllPlaces(int width)
    {
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = width, Height = 844, Content = shell };
        try
        {
            window.Show(); shell.OpenStoryFor("utility"); Dispatcher.UIThread.RunJobs();
            var story = Assert.IsType<StoryScreen>(shell.CurrentScreen);
            story.FindControl<CheckBox>("AnimateSelection")!.BringIntoView(); Dispatcher.UIThread.RunJobs();
            Assert.True(story.FindControl<ScrollViewer>("StoryScroll")!.Offset.Y > 100);
            ShellNavigationTests.Navigate(shell, ShellDestination.Home);
            var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            state.SelectPlace(state.Home.Places[0].Id); state.OpenPage(); Dispatcher.UIThread.RunJobs();
            var action = home.FindControl<PlacePage>("PlacePage")!.FindControl<Button>("OpenStory")!;
            action.BringIntoView(); Dispatcher.UIThread.RunJobs(); Assert.True(action.Focus()); Activate(window);
            Assert.Same(story, shell.CurrentScreen); Assert.Null(story.State.SelectedPlaceId);
            Assert.Contains("Today", story.FindControl<TextBlock>("DateLabel")!.Text);
            AssertInside(story.FindControl<TextBlock>("DateLabel")!, window); AssertInside(Filter(story, null), window);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(390)]
    [InlineData(1194)]
    public void OrdinaryStoryTabReturnRetainsItsDatePlaceAndNonzeroReadingPosition(int width)
    {
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = width, Height = 844, Content = shell };
        try
        {
            window.Show(); ShellNavigationTests.Navigate(shell, ShellDestination.Story);
            var story = Assert.IsType<StoryScreen>(shell.CurrentScreen);
            story.FindControl<Button>("PreviousDate")!.Focus(); Activate(window);
            story.State.SelectPlace("utility"); Dispatcher.UIThread.RunJobs();
            var date = story.State.Day.Date;
            var scroll = story.FindControl<ScrollViewer>("StoryScroll")!;
            scroll.Offset = new Vector(0, 200); Dispatcher.UIThread.RunJobs(); Assert.Equal(200, scroll.Offset.Y);
            ShellNavigationTests.Navigate(shell, ShellDestination.Home);
            ShellNavigationTests.Navigate(shell, ShellDestination.Story);
            Assert.Same(story, shell.CurrentScreen); Assert.Equal(date, story.State.Day.Date);
            Assert.Equal("utility", story.State.SelectedPlaceId); Assert.Equal(200, scroll.Offset.Y);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("focus")]
    [InlineData("navigate")]
    [InlineData("hide")]
    [InlineData("detach")]
    [InlineData("time")]
    public void ADeferredHistoryEntryCannotOverrideAnInterveningContextChange(string change)
    {
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show(); ShellNavigationTests.Navigate(shell, ShellDestination.Story);
            var story = Assert.IsType<StoryScreen>(shell.CurrentScreen);
            shell.Navigation.ButtonFor(ShellDestination.Story).Focus();
            shell.OpenStoryFor("utility");
            var scroll = story.FindControl<ScrollViewer>("StoryScroll")!;
            scroll.Offset = new Vector(0, 200);
            if (change == "focus") { shell.Navigation.ButtonFor(ShellDestination.More).Focus(); }
            if (change == "navigate") { shell.Navigate(ShellDestination.Home); shell.Navigate(ShellDestination.Story); }
            if (change == "hide") { story.IsVisible = false; story.IsVisible = true; }
            if (change == "detach")
            {
                var host = shell.FindControl<ContentControl>("ScreenHost")!;
                host.Content = null; host.Content = story;
            }
            if (change == "time") { story.State.SelectFraction(.25); }
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(200, scroll.Offset.Y);
            if (change == "focus") { Assert.Same(shell.Navigation.ButtonFor(ShellDestination.More), window.FocusManager!.GetFocusedElement()); }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RoutedKeyboardInputCancelsPendingEntryWithoutAChangeOfFocusOrTime()
    {
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show(); ShellNavigationTests.Navigate(shell, ShellDestination.Story);
            var story = Assert.IsType<StoryScreen>(shell.CurrentScreen);
            var day = story.FindControl<RadioButton>("DayMode")!; Assert.True(day.Focus());
            shell.OpenStoryFor("utility");
            var instant = story.State.SelectedInstant;
            var scroll = story.FindControl<ScrollViewer>("StoryScroll")!; scroll.Offset = new Vector(0, 200);
            // The raw headless key helper flushes Loaded first. Route this input synchronously to test the pending boundary.
            day.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F1, PhysicalKey = PhysicalKey.F1 });
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(200, scroll.Offset.Y); Assert.Equal(instant, story.State.SelectedInstant);
            Assert.Same(day, window.FocusManager!.GetFocusedElement());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LiveTextAndViewportReflowKeepsTheLateFilterVisibleWithoutMovingTheReader()
    {
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = 1194, Height = 844, Content = shell };
        try
        {
            window.Show(); ShellNavigationTests.Navigate(shell, ShellDestination.Story);
            var story = Assert.IsType<StoryScreen>(shell.CurrentScreen); story.State.SelectPlace("utility"); Dispatcher.UIThread.RunJobs();
            var scroll = story.FindControl<ScrollViewer>("StoryScroll")!; scroll.Offset = new Vector(0, 200);
            var focus = shell.Navigation.ButtonFor(ShellDestination.Story); Assert.True(focus.Focus());
            window.Width = 390; Dispatcher.UIThread.RunJobs(); AssertFilterVisible(story);
            CompanionTypography.Apply(size => size * 2); Dispatcher.UIThread.RunJobs(); AssertFilterVisible(story);
            window.Width = 1194; Dispatcher.UIThread.RunJobs(); AssertFilterVisible(story);
            Assert.Equal(200, scroll.Offset.Y); Assert.Same(focus, window.FocusManager!.GetFocusedElement());
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnOversizedSelectedFilterShowsItsBeginningAndSettles(bool large)
    {
        CompanionTypography.Apply(size => large ? size * 2 : size);
        var original = SampleHome.Create();
        var home = new Lucia.Companion.Core.HomeTopology(original.Id, original.Name,
            original.Places.Select(place => place.Id == "utility" ? place with { Name = new string('M', 200) } : place).ToArray(), original.Connections, original.Bindings);
        var story = new StoryScreen(SampleStory.Navigator(home));
        var window = new Window { Width = 390, Height = 844, Content = story };
        try
        {
            window.Show(); story.State.SelectPlace("utility"); Dispatcher.UIThread.RunJobs();
            var chip = Filter(story, "utility"); var strip = chip.GetVisualAncestors().OfType<ScrollViewer>().First();
            Assert.True(chip.Bounds.Width > strip.Viewport.Width);
            Assert.Equal(0, chip.TranslatePoint(default, strip)!.Value.X, 1);
            var offset = strip.Offset.X; Assert.True(double.IsFinite(offset));
            story.State.SelectFraction(.25); Dispatcher.UIThread.RunJobs();
            story.State.SelectFraction(.5); Dispatcher.UIThread.RunJobs();
            Assert.Equal(offset, strip.Offset.X);
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    [AvaloniaFact]
    public void ExplicitConnectedHistoryEntryKeepsTheUnavailableState()
    {
        var state = new HomeViewModel(new Lucia.Companion.Core.HomeTopology("connected", "Our home", [], []), isDemo: false);
        var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show(); shell.OpenStoryFor(null); Dispatcher.UIThread.RunJobs();
            Assert.IsType<HistoryPendingScreen>(shell.CurrentScreen);
            Assert.DoesNotContain(window.GetVisualDescendants(), item => item is StoryScreen);
        }
        finally { window.Close(); }
    }

    private static void AssertFilterVisible(StoryScreen story)
    {
        var chip = Filter(story, "utility"); var strip = chip.GetVisualAncestors().OfType<ScrollViewer>().First();
        var point = chip.TranslatePoint(default, strip)!.Value;
        Assert.True(point.X >= -.1 && point.X + chip.Bounds.Width <= strip.Viewport.Width + .1);
    }

    private static Button Filter(StoryScreen story, string? id) => story.FindControl<PlaceFilterBar>("PlaceFilters")!
        .GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("theme") && Equals(button.Tag, id));

    private static void Activate(Window window)
    {
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static void AssertInside(Control target, Window window)
    {
        Assert.True(target.IsEffectivelyVisible);
        var location = target.TranslatePoint(default, window)!.Value;
        Assert.True(location.X >= -.1 && location.Y >= -.1 && location.X + target.Bounds.Width <= window.Bounds.Width + .1
            && location.Y + target.Bounds.Height <= window.Bounds.Height + .1,
            $"{target.Name ?? target.GetType().Name} at {location}, size {target.Bounds.Size} outside {window.Bounds.Size}.");
        foreach (var scroll in target.GetVisualAncestors().OfType<ScrollViewer>())
        {
            var point = target.TranslatePoint(default, scroll)!.Value;
            Assert.True(point.X >= -.1 && point.Y >= -.1 && point.X + target.Bounds.Width <= scroll.Viewport.Width + .1
                && point.Y + target.Bounds.Height <= scroll.Viewport.Height + .1,
                $"{target.Name ?? target.GetType().Name} at {point}, size {target.Bounds.Size} outside {scroll.Name} viewport {scroll.Viewport}.");
        }
    }
}

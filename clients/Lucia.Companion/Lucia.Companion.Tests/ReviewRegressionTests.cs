using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

/// <summary>Regressions from the review of the W5 app-only track.</summary>
public sealed class ReviewRegressionTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(6, 3, 3, "Utility first. The other 3 places have nothing to report.")]
    [InlineData(6, 3, 1, "Utility first. 1 of the other 3 places has nothing to report.")]
    [InlineData(6, 3, 0, "Utility first. The other 3 places report changes or activity.")]
    public void SublineOnlyCountsQuietPlacesAsHavingNothingToReport(int places, int attention, int quiet, string expected)
    {
        Assert.Equal(expected, HomeAttention.Subline("Utility", attention, places, isDemo: true, quietCount: quiet));
    }

    [Fact]
    public void WithNothingFlaggedTheDemoStillCountsPlacesThatReportSomething()
    {
        Assert.Equal("Nothing in this sample needs you. 2 of 6 places report changes or activity.", HomeAttention.Subline(null, 0, 6, isDemo: true, quietCount: 4));
        Assert.Equal("None of the 6 places in this sample has anything to report.", HomeAttention.Subline(null, 0, 6, isDemo: true, quietCount: 6));
    }

    [AvaloniaFact]
    public void TheDemoHeaderNoLongerContradictsChangedAndWorkingPlaces()
    {
        var view = new HomeScreen(new HomeViewModel(SampleHome.Create()));
        var window = new Window { Width = 390, Height = 844, Content = view };
        try
        {
            window.Show();
            Assert.Equal("Utility. A sample water sensor reports a leak.", view.FindControl<TextBlock>("AttentionSummary")!.Text);
        }
        finally { window.Close(); }
    }

    [Fact]
    public void AnUnavailablePlaceWithSomeCurrentReadingsSaysSo()
    {
        var place = new Place("p", "Hall", PlaceState.Unavailable, "Sources are missing.",
            [new("Hall light · Light", "On", ObservationQuality.Current, Noon) { Role = ObservationRole.Light }, new("Contact", null, ObservationQuality.Unavailable, null)]);
        Assert.Equal("Some sources report now, but at least one is missing, stale or unavailable, so the place isn't shown as normal.", PlaceExplanation.Body(place));
    }

    [Fact]
    public void NoDataTimesAreLabelledUtcLikeEveryOtherTime()
    {
        var place = new Place("p", "Bedroom", PlaceState.Unavailable, "Gone quiet.",
            [new("Temperature", "19.2 °C", ObservationQuality.Unavailable, Noon.AddHours(-2))]);
        Assert.Equal("No data since 10:00 UTC", PlaceCaption.Status(place));
    }

    [AvaloniaFact]
    public void RebuildingHomeMidHoldEndsTheHoldAndClearsTheArmedAction()
    {
        var state = new HomeViewModel(SampleHome.Create());
        var home = new HomeScreen(state);
        var window = new Window { Width = 390, Height = 844, Content = home };
        try
        {
            window.Show();
            var constellation = home.FindControl<HomeConstellation>("Constellation")!;
            constellation.HoldDelay = TimeSpan.Zero;
            var porch = Orb(constellation, "porch");
            window.MouseDown(Center(porch, window), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            var bloom = home.FindControl<BloomActions>("PlaceActions")!;
            var history = bloom.FindControl<Button>("HistoryButton")!;
            window.MouseMove(Center(history, window));
            Assert.Contains("armed", history.Classes);
            Assert.True(constellation.IsHolding);

            // A refresh that changes the layout (here a place is removed) rebuilds the places mid-gesture.
            var sample = SampleHome.Create();
            state.ReplaceHome(new HomeTopology(sample.Id, sample.Name, sample.Places.Where(place => place.Id != "kitchen"),
                sample.Connections.Where(edge => edge.FromPlaceId != "kitchen" && edge.ToPlaceId != "kitchen")), notifyPreferences: false);
            Dispatcher.UIThread.RunJobs();
            Assert.False(constellation.IsHolding);
            Assert.DoesNotContain("armed", history.Classes);
            window.MouseUp(new Point(4, 4), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.IsType<HomeScreen>(window.Content);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AHoldReleasedAwayFromThePlaceDoesNotSwallowTheNextActivation()
    {
        var state = new HomeViewModel(SampleHome.Create());
        var home = new HomeScreen(state);
        var window = new Window { Width = 390, Height = 844, Content = home };
        try
        {
            window.Show();
            var constellation = home.FindControl<HomeConstellation>("Constellation")!;
            constellation.HoldDelay = TimeSpan.Zero;
            var porch = Orb(constellation, "porch");
            window.MouseDown(Center(porch, window), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            var pin = home.FindControl<BloomActions>("PlaceActions")!.FindControl<Button>("PinButton")!;
            window.MouseMove(Center(pin, window));
            window.MouseUp(Center(pin, window), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.True(state.IsPinned("porch"));
            Assert.False(state.IsActionsOpen);

            Orb(constellation, "porch").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(state.IsDetailOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PlacesDoNotAlsoRaiseTheTouchHoldGesture()
    {
        var home = new HomeScreen(new HomeViewModel(SampleHome.Create()));
        Assert.All(home.FindControl<HomeConstellation>("Constellation")!.Orbs, orb => Assert.False(orb.GetValue(InputElement.IsHoldingEnabledProperty)));
    }

    [AvaloniaFact]
    public void WeekStatsDescribeTheWeekAndRowsSurviveAnUnrelatedRefresh()
    {
        var view = new StoryScreen(SampleStory.Navigator(SampleHome.Create()));
        var window = new Window { Width = 1194, Height = 834, Content = view };
        try
        {
            window.Show();
            view.FindControl<RadioButton>("WeekMode")!.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            var week = view.FindControl<StoryWeek>("Week")!;
            var expected = SampleStory.Dates.Sum(date => SampleStory.Day(SampleHome.Create(), date).Intervals.Count(interval => interval.Quality == StoryQuality.Recorded));
            Assert.Equal($"{expected} reports · 7 Lucia requests this week", view.FindControl<TextBlock>("StatsLine")!.Text);
            var first = week.Rows[0];
            MotionSettings.Current.ReduceMotion = false;
            MotionSettings.Current.ReduceMotion = true;
            Dispatcher.UIThread.RunJobs();
            Assert.Same(first, week.Rows[0]);
        }
        finally { window.Close(); }
    }

    private static PlaceOrb Orb(HomeConstellation constellation, string id) => constellation.Orbs.Single(orb => (string)orb.Tag! == id);

    private static Point Center(Control control, Window window)
    {
        var target = control is PlaceOrb orb ? orb.GetVisualDescendants().OfType<Control>().First(item => item.Name == "Orb") : control;
        return target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
    }
}

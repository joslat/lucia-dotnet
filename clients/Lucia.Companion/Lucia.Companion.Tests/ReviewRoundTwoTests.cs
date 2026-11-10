using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Configuration;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

/// <summary>Regressions from the second review round of the W5 app-only track.</summary>
public sealed class ReviewRoundTwoTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AZoneChangedAfterReviewMakesAdoptionAskForAFreshReview()
    {
        var layout = SharedLayoutAdoptionPlanTests.Layout();
        var porchId = layout.Places[0].Id;
        var original = new HomeTopology(layout.HomeId, "Personal home", [new(porchId, "Local porch", PlaceState.Quiet, "Prior", []) { Zone = PlaceZone.Inside }], []);
        var plan = SharedLayoutAdoptionPlan.Create(original, layout, SharedLayoutAdoptionPlanTests.Identity());
        var edited = new HomeTopology(original.Id, original.Name, original.Places.Select(place => place with { Zone = PlaceZone.Outside }), original.Connections);
        Assert.Throws<InvalidOperationException>(() => { plan.ApplyTo(edited, layout, SharedLayoutAdoptionPlanTests.Identity()); });
    }

    [AvaloniaFact]
    public void ARefreshWithTheSameLayoutUpdatesPlacesInPlaceAndKeepsBloomAndTheHold()
    {
        var state = new HomeViewModel(SampleHome.Create());
        var home = new HomeScreen(state);
        var window = new Window { Width = 390, Height = 844, Content = home };
        try
        {
            window.Show();
            var constellation = home.FindControl<HomeConstellation>("Constellation")!;
            constellation.HoldDelay = TimeSpan.Zero;
            var porch = constellation.Orbs.Single(orb => (string)orb.Tag! == "porch");
            window.MouseDown(Center(porch, window), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.True(constellation.IsHolding);

            var refreshed = new HomeTopology(state.Home.Id, state.Home.Name, state.Home.Places.Select(place => place.Id == "porch"
                ? place with { State = PlaceState.Quiet, Reason = "Recent source reports." } : place), state.Home.Connections);
            state.ReplaceHome(refreshed, notifyPreferences: false);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(porch, constellation.Orbs.Single(orb => (string)orb.Tag! == "porch"));
            Assert.True(constellation.IsHolding);
            Assert.True(state.IsActionsOpen);
            Assert.Equal("Front door closed", porch.Caption);
            Assert.DoesNotContain("waiting", porch.Classes);
            Assert.Contains("quiet", porch.Classes);
            window.MouseUp(new Point(4, 4), MouseButton.Left);
        }
        finally { window.Close(); }
    }

    [Fact]
    public void AChangedPlaceSaysWhatChanged()
    {
        var place = new Place("living", "Living room", PlaceState.Changed, "Motion sensor reports activity, not presence.",
        [
            new("Lighting", "Off", ObservationQuality.Current, Noon) { Role = ObservationRole.Light },
            new("Temperature", "21 °C", ObservationQuality.Current, Noon) { Role = ObservationRole.Temperature },
            new("Hall motion · Motion", "Active", ObservationQuality.Current, Noon) { Role = ObservationRole.Motion },
        ]);
        Assert.Equal("Changed · Hall motion active", PlaceCaption.Status(place));
        Assert.StartsWith("Hall motion active, reported at 12:00 UTC.", PlaceExplanation.Body(place), StringComparison.Ordinal);
    }

    [Fact]
    public void DateStepsSkipDaysWithoutStory()
    {
        var home = SampleHome.Create();
        DateOnly[] dates = [new(2026, 10, 20), new(2026, 10, 22), new(2026, 10, 23)];
        var navigator = new StoryNavigator(dates, dates[^1], date => SampleStory.Day(home, date));
        navigator.Previous();
        Assert.Equal(dates[1], navigator.Date);
        navigator.Previous();
        Assert.Equal(dates[0], navigator.Date);
        Assert.False(navigator.CanGoPrevious);
        navigator.Next();
        Assert.Equal(dates[1], navigator.Date);
    }

    [AvaloniaFact]
    public void AllStaleReportsAreNotCalledLive()
    {
        var setup = new HomeTopology("home", "Our home",
            [new("hall", "Hall", PlaceState.Unavailable, "Some configured reports are stale.", [new("Hall · Temperature", "20 °C", ObservationQuality.Stale, Noon.AddHours(-1))])], [],
            [new ObservationBinding("hall", "sensor.hall", ObservationRole.Temperature, "hall")]);
        var state = new HomeViewModel(setup, isDemo: false);
        var view = new HomeScreen(state);
        var window = new Window { Width = 390, Height = 844, Content = view };
        try
        {
            window.Show();
            state.SetReportTime(Noon);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Reports stale", view.FindControl<TextBlock>("HomeModeLabel")!.Text);
            Assert.False(view.FindControl<Control>("LiveDot")!.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ThePlaceChartSaysItsDayIsUtcAndTheStoryLinkOpensTheWholeHome()
    {
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = 390, Height = 844, Content = shell };
        try
        {
            window.Show();
            var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            state.SelectPlace("bedroom");
            Dispatcher.UIThread.RunJobs();
            var page = home.FindControl<PlacePage>("PlacePage")!;
            Assert.StartsWith("Sample history · UTC day", page.FindControl<TextBlock>("ChartCaption")!.Text, StringComparison.Ordinal);
            Assert.Contains("at 10:00 UTC", page.FindControl<ReadingChart>("TodayChart")!.GetValue(AutomationProperties.NameProperty), StringComparison.Ordinal);
            page.FindControl<Button>("OpenStory")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var story = Assert.IsType<StoryScreen>(shell.CurrentScreen);
            Assert.Null(story.State.SelectedPlaceId);
        }
        finally { window.Close(); }
    }

    [Fact]
    public void StoryAndHomeShareOneNeedsYouRule()
    {
        var home = SampleHome.Create();
        Assert.Equal(new HomeViewModel(home).AttentionPlaces.Count, SampleStory.Navigator(home).AttentionCount);
        Assert.All(home.Places, place => Assert.Equal(place.State is PlaceState.Problem or PlaceState.Waiting or PlaceState.Unavailable, HomeAttention.NeedsYou(place)));
    }

    [Fact]
    public void OpeningActionsForAPlaceNotifiesOnce()
    {
        var state = new HomeViewModel(SampleHome.Create());
        var changes = 0;
        state.PropertyChanged += (_, _) => changes++;
        state.OpenActionsFor("porch");
        Assert.Equal(1, changes);
        Assert.Equal("porch", state.SelectedPlace?.Id);
        Assert.True(state.IsActionsOpen);
        Assert.False(state.IsDetailOpen);
    }

    private static Point Center(PlaceOrb orb, Window window)
    {
        var target = orb.GetVisualDescendants().OfType<Control>().First(item => item.Name == "Orb");
        return target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
    }
}

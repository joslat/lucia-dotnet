using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

/// <summary>Regressions from the third review round, which looked at the first two rounds' fixes.</summary>
public sealed class ReviewRoundThreeTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [AvaloniaFact]
    public void BloomFollowsItsPlaceWhenALayoutChangeRebuildsThePlaces()
    {
        var state = new HomeViewModel(SampleHome.Create());
        var home = new HomeScreen(state);
        var window = new Window { Width = 390, Height = 844, Content = home };
        try
        {
            window.Show();
            state.OpenActionsFor("porch");
            Dispatcher.UIThread.RunJobs();
            var popup = home.FindControl<Popup>("ActionPopup")!;
            Assert.True(popup.IsOpen, $"actions {state.IsActionsOpen} target {popup.PlacementTarget} selected {state.SelectedPlace?.Id}");
            var sample = SampleHome.Create();
            state.ReplaceHome(new HomeTopology(sample.Id, sample.Name, sample.Places.Where(place => place.Id != "kitchen"),
                sample.Connections.Where(edge => edge.FromPlaceId != "kitchen" && edge.ToPlaceId != "kitchen")), notifyPreferences: false);
            Dispatcher.UIThread.RunJobs();
            // Either Bloom closed cleanly with its old anchor (Avalonia closes a popup whose target leaves the tree),
            // or it is still open on the new control for its place. Never open on a detached control.
            if (popup.IsOpen)
            {
                var anchor = Assert.IsAssignableFrom<Control>(popup.PlacementTarget);
                Assert.True(anchor.IsAttachedToVisualTree());
                Assert.True(state.IsActionsOpen);
            }
            else
            {
                Assert.False(state.IsActionsOpen);
                Assert.False(home.FindControl<Border>("BloomShade")!.IsVisible);
            }
        }
        finally { window.Close(); }
    }

    [Fact]
    public void AChangeIsTimedByTheReadingThatChanged()
    {
        var place = new Place("garden", "Garden", PlaceState.Changed, "A contact reading changed.",
        [
            new("Gate contact", "Open", ObservationQuality.Current, Noon.AddMinutes(-48)) { Role = ObservationRole.Contact },
            new("Temperature", "11 °C", ObservationQuality.Current, Noon.AddMinutes(-5)) { Role = ObservationRole.Temperature },
        ]);
        Assert.StartsWith("Gate open, reported at 11:12 UTC.", PlaceExplanation.Body(place), StringComparison.Ordinal);
    }

    [Fact]
    public void APartlyUnavailablePlaceNamesTheSourceThatWentQuiet()
    {
        var place = new Place("hall", "Hall", PlaceState.Unavailable, "Some configured reports are stale.",
        [
            new("Hall light · Light", "On", ObservationQuality.Current, Noon) { Role = ObservationRole.Light },
            new("Hall temperature · Temperature", "20 °C", ObservationQuality.Stale, Noon.AddHours(-3)) { Role = ObservationRole.Temperature },
        ]);
        Assert.Equal("Some sources report now, but Hall temperature · Temperature last read 20 °C at 09:00 UTC and isn't current, so the place isn't shown as normal.",
            PlaceExplanation.Body(place));
    }

    [AvaloniaFact]
    public void ABoundHomeWithNothingCurrentIsStaleButAnUnboundOneIsLive()
    {
        var unbound = new HomeViewModel(new HomeTopology("home", "Our home", [new("hall", "Hall", PlaceState.Unavailable, "No bindings.", [])], []), isDemo: false);
        var bound = new HomeViewModel(new HomeTopology("home", "Our home", [new("hall", "Hall", PlaceState.Unavailable, "No reports.", [])], [],
            [new ObservationBinding("hall", "sensor.hall", ObservationRole.Temperature, "hall")]), isDemo: false);
        foreach (var (state, expected) in new[] { (unbound, "Live"), (bound, "Reports stale") })
        {
            var view = new HomeScreen(state);
            var window = new Window { Width = 390, Height = 844, Content = view };
            try
            {
                window.Show();
                state.SetReportTime(Noon);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(expected, view.FindControl<TextBlock>("HomeModeLabel")!.Text);
            }
            finally { window.Close(); }
        }
    }

    [AvaloniaFact]
    public void AHomeOfferedDuringARebuildIsDrawnAfterIt()
    {
        var constellation = new HomeConstellation();
        var window = new Window { Width = 390, Height = 844, Content = constellation };
        try
        {
            window.Show();
            var first = SampleHome.Create();
            var second = new HomeTopology(first.Id, first.Name, first.Places.Where(place => place.Id != "kitchen"), []);
            var third = new HomeTopology(first.Id, first.Name, first.Places.Where(place => place.Id != "garden"), []);
            constellation.SetHome(first);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            // Simulate an event during the rebuild offering a newer home.
            constellation.HoldCancelled += () => constellation.SetHome(third);
            constellation.HoldDelay = TimeSpan.Zero;
            var porch = constellation.Orbs.Single(orb => (string)orb.Tag! == "porch").GetVisualDescendants().OfType<Control>().First(item => item.Name == "Orb");
            window.MouseDown(porch.TranslatePoint(new Point(porch.Bounds.Width / 2, porch.Bounds.Height / 2), window)!.Value, Avalonia.Input.MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.True(constellation.IsHolding);
            constellation.SetHome(second);
            Assert.Equal(third.Places.Select(place => place.Id).Order(), constellation.Orbs.Select(orb => (string)orb.Tag!).Order());
        }
        finally { window.Close(); }
    }
}

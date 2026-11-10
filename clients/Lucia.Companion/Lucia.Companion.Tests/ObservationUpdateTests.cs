using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class ObservationUpdateTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);
    private static Observation Reading(string value, ObservationQuality quality = ObservationQuality.Current, DateTimeOffset? at = null) => new("Temperature", value, quality, at ?? At) { Role = ObservationRole.Temperature };
    private static Place Room(params Observation[] readings) => new("room", "Living room", PlaceState.Quiet, "Reported readings", readings);
    private static HomeTopology Home(Place place, string source = "sensor.temperature") => new("home", "Home", [place], [], [new(place.Id, source, ObservationRole.Temperature, place.Id)]);

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefreshRetainsTheReadingRowAndShowsTheExactNewValueImmediately(bool page)
    {
        var first = Room(Reading("21.4 °C"));
        Control view = page ? new PlacePage() : new PlaceDetail();
        void Apply(Place place) { if (view is PlacePage full) { full.SetPlace(place, Home(place), false, At); } else { ((PlaceDetail)view).SetPlace(place, Home(place)); } }
        Apply(first);
        var window = new Window { Width = 390, Height = 900, Content = view };
        try
        {
            window.Show();
            var row = Assert.Single(view.GetVisualDescendants().OfType<ObservationRow>());
            var value = row.FindControl<TextBlock>("ValueText")!;
            Apply(Room(Reading("22.9 °C")));
            Assert.Same(row, Assert.Single(view.GetVisualDescendants().OfType<ObservationRow>()));
            Assert.Same(value, row.FindControl<TextBlock>("ValueText"));
            Assert.Equal("22.9 °C", value.Text);
            Assert.Contains("22.9 °C", AutomationProperties.GetName(row));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NewValuesAreEmphasizedButTimestampOnlyRefreshesStayQuiet()
    {
        var motion = MotionSettings.Current;
        motion.ReduceMotion = false;
        var row = new ObservationRow(Reading("21.4 °C"));
        var window = new Window { Width = 390, Height = 240, Content = row };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, row.EmphasisOpacity);
            row.Update(Reading("21.4 °C", at: At.AddMinutes(1)));
            Assert.Equal(0, row.EmphasisOpacity);
            Assert.Contains("10:01", row.FindControl<TextBlock>("TimeText")!.Text);
            row.Update(Reading("22.9 °C", at: At.AddMinutes(2)));
            Assert.Equal("22.9 °C", row.FindControl<TextBlock>("ValueText")!.Text);
            Assert.True(row.EmphasisOpacity > 0);
            motion.ReduceMotion = true;
            Assert.Equal(0, row.EmphasisOpacity);
            Assert.Null(row.Transitions);
            row.Update(Reading("23.1 °C"));
            Assert.Equal("23.1 °C", row.FindControl<TextBlock>("ValueText")!.Text);
            Assert.Equal(0, row.EmphasisOpacity);
        }
        finally { motion.ReduceMotion = true; motion.Resume(); window.Close(); }
    }

    [AvaloniaFact]
    public void AQualityChangeImmediatelyMovesTheValueIntoLastKnownAndPauseClearsEmphasis()
    {
        MotionSettings.Current.ReduceMotion = false;
        var row = new ObservationRow(Reading("21.4 °C"));
        var window = new Window { Content = row };
        try
        {
            window.Show();
            row.Update(Reading("21.4 °C", ObservationQuality.Stale));
            Assert.Equal("Stale", row.FindControl<TextBlock>("ValueText")!.Text);
            Assert.Contains("Last known: 21.4 °C", row.FindControl<TextBlock>("PreviousText")!.Text);
            Assert.True(row.EmphasisOpacity > 0);
            MotionSettings.Current.Pause();
            Assert.Equal(0, row.EmphasisOpacity);
            Assert.Equal("Stale", row.FindControl<TextBlock>("ValueText")!.Text);
            MotionSettings.Current.Resume();
            Assert.Equal(0, row.EmphasisOpacity);
        }
        finally { MotionSettings.Current.ReduceMotion = true; MotionSettings.Current.Resume(); window.Close(); }
    }

    [AvaloniaFact]
    public void ChangedBindingsStartFreshAndRemovedRowsDisappearImmediately()
    {
        var first = Room(Reading("21.4 °C"));
        var detail = new PlaceDetail();
        detail.SetPlace(first, Home(first));
        var window = new Window { Content = detail };
        try
        {
            window.Show();
            var old = Assert.Single(detail.GetVisualDescendants().OfType<ObservationRow>());
            detail.SetPlace(first, Home(first, "sensor.reassigned_temperature"));
            var fresh = Assert.Single(detail.GetVisualDescendants().OfType<ObservationRow>());
            Assert.NotSame(old, fresh);
            Assert.Equal(0, fresh.EmphasisOpacity);
            var empty = Room();
            detail.SetPlace(empty, Home(empty, "sensor.reassigned_temperature"));
            Assert.Empty(detail.GetVisualDescendants().OfType<ObservationRow>());
            Assert.Equal(0, fresh.EmphasisOpacity);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DuplicateLabelsAreNotTreatedAsEvidenceOfSourceIdentity()
    {
        var first = Room(Reading("21.4 °C"), Reading("18.2 °C"));
        var detail = new PlaceDetail();
        detail.SetPlace(first, Home(first));
        var window = new Window { Content = detail };
        try
        {
            window.Show();
            var old = detail.GetVisualDescendants().OfType<ObservationRow>().ToArray();
            var changed = Room(Reading("18.3 °C"), Reading("21.5 °C"));
            detail.SetPlace(changed, Home(changed));
            var current = detail.GetVisualDescendants().OfType<ObservationRow>().ToArray();
            Assert.Equal(2, current.Length);
            Assert.All(current, row => Assert.DoesNotContain(row, old));
            Assert.Equal(["18.3 °C", "21.5 °C"], current.Select(row => row.FindControl<TextBlock>("ValueText")!.Text));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void HidingOrDetachingAnUpdatingRowStopsItsEffectWithoutReplayingIt()
    {
        MotionSettings.Current.ReduceMotion = false;
        var row = new ObservationRow(Reading("21.4 °C"));
        var parent = new Border { Child = row };
        var window = new Window { Content = parent };
        try
        {
            window.Show();
            row.Update(Reading("22.9 °C"));
            Assert.True(row.EmphasisOpacity > 0);
            parent.IsVisible = false;
            Assert.Equal(0, row.EmphasisOpacity);
            Assert.Null(row.Transitions);
            parent.IsVisible = true;
            Assert.Equal(0, row.EmphasisOpacity);
            row.Update(Reading("23.1 °C"));
            Assert.True(row.EmphasisOpacity > 0);
            window.Close();
            Assert.Equal(0, row.EmphasisOpacity);
            Assert.Null(row.Transitions);
        }
        finally { MotionSettings.Current.ReduceMotion = true; window.Close(); }
    }

    [AvaloniaFact]
    public async Task RapidChangesKeepTheLatestValueAndDoNotQueueOldEffects()
    {
        MotionSettings.Current.ReduceMotion = false;
        var row = new ObservationRow(Reading("21.4 °C"));
        var window = new Window { Content = row };
        try
        {
            window.Show();
            row.Update(Reading("22.9 °C"));
            row.Update(Reading("23.1 °C"));
            row.Update(Reading("23.4 °C"));
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(440, TestContext.Current.CancellationToken);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
            Assert.Equal("23.4 °C", row.FindControl<TextBlock>("ValueText")!.Text);
            Assert.Equal(0, row.EmphasisOpacity, precision: 2);
        }
        finally { MotionSettings.Current.ReduceMotion = true; window.Close(); }
    }

    [AvaloniaFact]
    public async Task EmphasisRendersIntermediateFramesThenStopsWithoutChangingTheReading()
    {
        MotionSettings.Current.ReduceMotion = false;
        var row = new ObservationRow(Reading("21.4 °C"));
        var window = new Window { Width = 390, Height = 180, Content = row };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            row.Update(Reading("22.9 °C")); Dispatcher.UIThread.RunJobs();
            var frames = new List<double>();
            for (var index = 0; index < 14; index++)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
                frames.Add(row.EmphasisOpacity);
                Assert.Equal("22.9 °C", row.FindControl<TextBlock>("ValueText")!.Text);
                if (index is 0 or 4 or 13) { NativeCapture.Save(window, $"ux-reading-update-{index}.png"); }
                await Task.Delay(35, TestContext.Current.CancellationToken);
            }
            Assert.Contains(frames, value => value > .01 && value < .7);
            Assert.Equal(0, frames[^1], precision: 2);
            window.Close();
            Assert.Equal(0, row.EmphasisOpacity);
        }
        finally { MotionSettings.Current.ReduceMotion = true; window.Close(); }
    }
}

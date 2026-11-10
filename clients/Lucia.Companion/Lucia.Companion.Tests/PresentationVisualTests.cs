using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class PresentationVisualTests
{
    [AvaloniaTheory]
    [InlineData(390, 844, false, false)]
    [InlineData(390, 844, true, false)]
    [InlineData(1194, 834, false, false)]
    [InlineData(1194, 834, true, false)]
    [InlineData(390, 844, false, true)]
    [InlineData(390, 844, true, true)]
    [InlineData(1194, 834, false, true)]
    [InlineData(1194, 834, true, true)]
    public async Task LoadingUpdatedReadingsAndStaleDataRemainReadable(int width, int height, bool dark, bool large)
    {
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        CompanionTypography.Apply(size => large ? size * 2 : size);
        MotionSettings.Current.ReduceMotion = true;
        var response = new TaskCompletionSource<ConnectionSession?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new TestCompanionConnection { HasSavedConnection = true, Restore = () => response.Task };
        var root = new CompanionRoot(service);
        var window = new Window { Width = width, Height = height, Content = root };
        var suffix = $"{width}-{(dark ? "dark" : "light")}-{(large ? "large" : "normal")}";
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            Assert.True(root.State.IsBusy);
            Assert.Contains(root.GetVisualDescendants().OfType<OperationProgress>(), item => item.IsBusy);
            NativeCapture.Save(window, $"ux-loading-{suffix}.png");
            response.SetResult(null); await root.PendingOperation;

            var source = SampleHome.Create();
            var place = source.Places.First(item => item.Observations.Any(reading => reading.Role == ObservationRole.Temperature && reading.IsCurrent));
            var original = place.Observations.First(reading => reading.Role == ObservationRole.Temperature && reading.IsCurrent);
            var page = new PlacePage();
            page.SetPlace(place, source, isDemo: true, SampleHome.CapturedAt);
            window.Content = new ScrollViewer { Content = page, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            Dispatcher.UIThread.RunJobs();
            var before = page.GetVisualDescendants().OfType<ObservationRow>().Single(row => row.Reading?.Role == ObservationRole.Temperature);
            if (large) { window.UpdateLayout(); before.BringIntoView(); Dispatcher.UIThread.RunJobs(); }
            NativeCapture.Save(window, $"ux-readings-ready-{suffix}.png");

            MotionSettings.Current.ReduceMotion = false;
            var updated = new Observation(original.Label, "22.9 °C", ObservationQuality.Current, original.ObservedAt, original.TimeBasis) { Role = original.Role };
            var changed = place with { Observations = place.Observations.Select(reading => ReferenceEquals(reading, original) ? updated : reading).ToArray() };
            page.SetPlace(changed, source, isDemo: true, SampleHome.CapturedAt);
            Dispatcher.UIThread.RunJobs();
            var current = page.GetVisualDescendants().OfType<ObservationRow>().Single(row => row.Reading?.Role == ObservationRole.Temperature);
            Assert.Same(before, current);
            Assert.Equal("22.9 °C", current.FindControl<TextBlock>("ValueText")!.Text);
            if (large) { window.UpdateLayout(); current.BringIntoView(); Dispatcher.UIThread.RunJobs(); }
            NativeCapture.Save(window, $"ux-readings-changed-{suffix}.png");

            MotionSettings.Current.ReduceMotion = true;
            var stale = new Observation(original.Label, "22.9 °C", ObservationQuality.Stale, original.ObservedAt, original.TimeBasis) { Role = original.Role };
            page.SetPlace(changed with { State = PlaceState.Unavailable, Reason = "The sample temperature report is no longer current. Its last-known value is retained.", Observations = changed.Observations.Select(reading => ReferenceEquals(reading, updated) ? stale : reading).ToArray() }, source, isDemo: true, SampleHome.CapturedAt.AddHours(1));
            Assert.Equal("Stale", current.FindControl<TextBlock>("ValueText")!.Text);
            Assert.Contains("22.9 °C", current.FindControl<TextBlock>("PreviousText")!.Text);
            if (large) { window.UpdateLayout(); current.BringIntoView(); Dispatcher.UIThread.RunJobs(); }
            NativeCapture.Save(window, $"ux-readings-stale-{suffix}.png");
            Assert.True(page.Bounds.Width <= width);
        }
        finally
        {
            response.TrySetResult(null); await root.PendingOperation;
            MotionSettings.Current.ReduceMotion = true;
            CompanionTypography.Apply(size => size);
            window.Close();
        }
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Lucia.Companion.App;
using Lucia.Companion.App.Screens;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class StoryScopeTests
{
    [AvaloniaTheory]
    [InlineData(390, false)]
    [InlineData(390, true)]
    [InlineData(1194, false)]
    [InlineData(1194, true)]
    public void APlaceWithoutSuppliedHistoryHasZeroPlaceTotalsAndQualifiedHomeAttention(int width, bool large)
    {
        CompanionTypography.Apply(size => large ? size * 2 : size);
        var screen = new StoryScreen(SampleStory.Navigator(SampleHome.Create()));
        var window = new Window { Width = width, Height = 844, Content = screen };
        try
        {
            window.Show(); screen.State.SelectPlace("utility"); Dispatcher.UIThread.RunJobs();
            Assert.Empty(screen.State.VisibleIntervals);
            Assert.Equal("0 reports · 0 Lucia requests for this place · Home now: 3 need you", screen.FindControl<TextBlock>("StatsLine")!.Text);
            if (!large) { Assert.Equal("now\nHome: 3 need you", screen.FindControl<TextBlock>("CenterCaption")!.Text); }
            NativeCapture.Save(window, $"story-scope-{width}-{large}.png");
            screen.FindControl<RadioButton>("WeekMode")!.IsChecked = true; Dispatcher.UIThread.RunJobs();
            var expected = SampleStory.Dates.Sum(date => SampleStory.Day(SampleHome.Create(), date).Intervals.Count(item => item.PlaceId == "utility" && item.Quality == Lucia.Companion.Core.StoryQuality.Recorded));
            Assert.Equal($"{expected} reports · 0 Lucia requests for this place this week", screen.FindControl<TextBlock>("StatsLine")!.Text);
            Assert.DoesNotContain("Home now", screen.FindControl<TextBlock>("StatsLine")!.Text);
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }
}

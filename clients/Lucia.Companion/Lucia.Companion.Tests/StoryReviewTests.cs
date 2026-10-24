using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
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

public sealed class StoryReviewTests
{
    [AvaloniaTheory]
    [InlineData(3, 29, 23)]
    [InlineData(10, 25, 25)]
    [InlineData(10, 24, 24)]
    public void TimelineRulerAlignsElapsedFractionsAndLabelsHomeOffsets(int month, int date, int hours)
    {
        var day = new StoryDay(SampleHome.Create(), new DateOnly(2026, month, date),
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich"), [], []);
        var ruler = new TimelineRuler(day);
        ruler.Measure(new Size(326, 48));
        ruler.Arrange(new Rect(0, 0, 326, 48));
        Assert.Equal(5, ruler.TickInstants.Count);
        Assert.Equal(day.Start, ruler.TickInstants[0]);
        Assert.Equal(day.End, ruler.TickInstants[^1]);
        Assert.Equal(TimeSpan.FromHours(hours / 4d), ruler.TickInstants[1] - day.Start);
        Assert.Equal(.75, day.Fraction(ruler.TickInstants[3]), 8);
        Assert.All(ruler.Children, child => Assert.InRange(child.Bounds.Right, 0, 326));
    }

    [AvaloniaFact]
    public void ReportsAndQualityCanBeReadWithoutHoverAndChaptersHaveIconRails()
    {
        var state = new StoryViewModel(SampleStory.Create(SampleHome.Create()));
        var screen = new StoryScreen(state);
        var window = new Window { Width = 390, Height = 844, Content = screen };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var report = state.Day.Intervals.First();
            var timelineButton = screen.GetVisualDescendants().OfType<PlaceTimeline>()
                .SelectMany(timeline => timeline.GetVisualDescendants().OfType<Button>())
                .Single(button => Equals(button.Tag, report.Id));
            Assert.Contains(state.Day.TimeLabel(report.End), AutomationProperties.GetName(timelineButton));
            Assert.Contains(report.Quality.ToString(), AutomationProperties.GetName(timelineButton));
            Assert.Contains(state.Day.TimeLabel(report.End), string.Join(" ", timelineButton.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text)));
            var chapters = screen.FindControl<ChapterList>("Chapters")!;
            Assert.Equal(state.VisibleChapters.Count, chapters.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Count());
            Assert.Contains(screen.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Last-known");
        }
        finally { window.Close(); }
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class StoryDetailTests
{
    [Fact]
    public void TodayKnowsItsNowAndStartsThere()
    {
        var navigator = SampleStory.Navigator(SampleHome.Create());
        Assert.Equal(SampleHome.CapturedAt, navigator.Day.Day.Now);
        Assert.Equal(SampleHome.CapturedAt, navigator.Day.SelectedInstant);
        Assert.Equal(3, navigator.AttentionCount);
        navigator.Previous();
        Assert.Null(navigator.Day.Day.Now);
        Assert.Throws<ArgumentOutOfRangeException>(() => new StoryDay(SampleHome.Create(), new DateOnly(2026, 10, 23), TimeZoneInfo.Utc, [], [])
            { Now = new DateTimeOffset(2026, 10, 25, 0, 0, 1, TimeSpan.Zero) });
    }

    [AvaloniaTheory]
    [InlineData(390, 844, false)]
    [InlineData(390, 844, true)]
    [InlineData(1194, 834, false)]
    [InlineData(1194, 834, true)]
    [InlineData(834, 1194, false)]
    public void TodayShowsNowTheFutureTheLuciaLaneAndChapterRanges(int width, int height, bool dark)
    {
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var shell = new CompanionShell(new HomeViewModel(SampleHome.Create()), store: null);
        var window = new Window { Width = width, Height = height, Content = shell };
        try
        {
            window.Show();
            ShellNavigationTests.Navigate(shell, ShellDestination.Story);
            var story = Assert.IsType<StoryScreen>(shell.CurrentScreen);
            Assert.Equal("14:00", story.FindControl<TextBlock>("CenterTime")!.Text);
            Assert.Equal("now · 3 need you", story.FindControl<TextBlock>("CenterCaption")!.Text);
            Assert.True(story.FindControl<StoryOrbit>("Orbit")!.HasFuture);

            var lane = story.FindControl<WeekDayLane>("LuciaLane")!;
            Assert.NotEmpty(lane.Intervals);
            Assert.All(lane.Intervals, interval => Assert.Equal(StoryKind.Lucia, interval.Kind));
            Assert.Equal("1 Lucia request in this day, from any place.", story.FindControl<TextBlock>("LuciaLaneCaption")!.Text);

            var rails = story.FindControl<ChapterList>("Chapters")!.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToArray();
            Assert.Contains("06:40–\n07:10", rails);

            var stats = story.FindControl<TextBlock>("StatsLine")!;
            Assert.Equal(story.IsExpanded, stats.IsVisible);
            Assert.Equal("5 reports · 1 Lucia request · 3 reasons for attention", stats.Text);
            if (width == 834)
            {
                Assert.True(story.IsExpanded);
                Assert.Equal(1, Grid.GetColumn(story.FindControl<StackPanel>("ChapterPanel")!));
            }
            NativeCapture.Save(window, $"story-detail-{width}-{(dark ? "dark" : "light")}.png");

            story.State.SelectPlace("garden");
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(lane.Intervals);
            Assert.Equal("No Lucia requests for this place in this day.", story.FindControl<TextBlock>("LuciaLaneCaption")!.Text);

            story.State.SelectPlace(null);
            story.FindControl<Button>("PreviousDate")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.False(story.FindControl<StoryOrbit>("Orbit")!.HasFuture);
            Assert.NotEqual("now · 3 need you", story.FindControl<TextBlock>("CenterCaption")!.Text);
            Assert.Equal("7 reports · 1 Lucia request", story.FindControl<TextBlock>("StatsLine")!.Text);
        }
        finally { window.Close(); }
    }
}

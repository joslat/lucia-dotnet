using Lucia.Companion.App;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class StoryNavigatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 24);

    [Fact]
    public void TheSampleWeekEndsTodayAndTodayStopsAtTheSampleClock()
    {
        var home = SampleHome.Create();
        Assert.Equal(Enumerable.Range(18, 7).Select(day => new DateOnly(2026, 10, day)), SampleStory.Dates);
        Assert.Equal(Today, SampleStory.Today);
        var today = SampleStory.Day(home, Today);
        Assert.NotEmpty(today.Intervals);
        Assert.All(today.Intervals, interval => Assert.True(interval.End <= SampleHome.CapturedAt));
        Assert.All(today.Chapters, chapter => Assert.True(chapter.End <= SampleHome.CapturedAt));
        // The original authored day is unchanged.
        var original = SampleStory.Create(home);
        Assert.Equal(original.Intervals, SampleStory.Day(home, new DateOnly(2026, 10, 23)).Intervals);
        Assert.Throws<ArgumentOutOfRangeException>(() => SampleStory.Day(home, new DateOnly(2026, 10, 25)));
    }

    [Fact]
    public void DayNavigationMovesWithinTheWeekAndKeepsThePlaceFilter()
    {
        var navigator = SampleStory.Navigator(SampleHome.Create());
        Assert.Equal(StoryMode.Day, navigator.Mode);
        Assert.Equal(Today, navigator.Date);
        Assert.Equal("Today · Sat 24 Oct", navigator.DateLabel);
        Assert.False(navigator.CanGoNext);
        Assert.True(navigator.CanGoPrevious);
        navigator.Day.SelectPlace("garden");
        navigator.Previous();
        Assert.Equal("Yesterday · Fri 23 Oct", navigator.DateLabel);
        Assert.Equal(new DateOnly(2026, 10, 23), navigator.Day.Day.Date);
        Assert.Equal("garden", navigator.Day.SelectedPlaceId);
        for (var step = 0; step < 5; step++) { navigator.Previous(); }
        Assert.Equal("Sun 18 Oct", navigator.DateLabel);
        Assert.False(navigator.CanGoPrevious);
        navigator.Previous();
        Assert.Equal(new DateOnly(2026, 10, 18), navigator.Date);
        navigator.Next();
        Assert.Equal(new DateOnly(2026, 10, 19), navigator.Date);
    }

    [Fact]
    public void WeekModeSummarisesEachDayAndOpensOne()
    {
        var navigator = SampleStory.Navigator(SampleHome.Create());
        var changes = 0;
        navigator.PropertyChanged += (_, _) => changes++;
        navigator.SetMode(StoryMode.Week);
        Assert.Equal("18–24 Oct", navigator.DateLabel);
        Assert.False(navigator.CanGoPrevious);
        Assert.False(navigator.CanGoNext);
        var week = navigator.Week;
        Assert.Equal(7, week.Count);
        Assert.Equal(["Sun 18", "Mon 19", "Tue 20", "Wed 21", "Thu 22", "Fri 23", "Sat 24 · today"], week.Select(day => day.Label));
        Assert.All(week, day => Assert.Equal(day.Day.Intervals.Count(interval => interval.Quality == StoryQuality.Recorded), day.ReportCount));
        navigator.Day.SelectPlace("garden");
        Assert.All(navigator.Week, day => Assert.All(day.Intervals, interval => Assert.Equal("garden", interval.PlaceId)));
        navigator.ShowDay(new DateOnly(2026, 10, 20));
        Assert.Equal(StoryMode.Day, navigator.Mode);
        Assert.Equal(new DateOnly(2026, 10, 20), navigator.Date);
        Assert.Equal("garden", navigator.Day.SelectedPlaceId);
        Assert.Equal(2, changes);
    }
}

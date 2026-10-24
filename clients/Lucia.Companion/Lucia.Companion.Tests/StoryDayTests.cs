using Lucia.Companion.App;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class StoryDayTests
{
    [Theory]
    [InlineData(2026, 3, 29, 23)]
    [InlineData(2026, 10, 25, 25)]
    [InlineData(2026, 10, 24, 24)]
    public void HomeCalendarDayUsesElapsedTimeAcrossClockChanges(int year, int month, int day, int hours)
    {
        var story = new StoryDay(SampleHome.Create(), new DateOnly(year, month, day),
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich"), [], []);
        Assert.Equal(TimeSpan.FromHours(hours), story.End - story.Start);
        var middle = story.Start.AddHours(hours / 2d);
        Assert.Equal(.5, story.Fraction(middle), 8);
        Assert.Equal(middle, story.InstantAt(.5));
        Assert.Equal(story.Start, story.InstantAt(-1));
        Assert.True(story.InstantAt(1) < story.End);
        Assert.Throws<ArgumentOutOfRangeException>(() => story.InstantAt(double.NaN));
    }

    [Fact]
    public void RepeatedLocalHourHasDifferentOffsetAndDifferentPosition()
    {
        var story = Day(new DateOnly(2026, 10, 25));
        var first = DateTimeOffset.Parse("2026-10-25T02:30:00+02:00");
        var second = DateTimeOffset.Parse("2026-10-25T02:30:00+01:00");
        Assert.NotEqual(story.TimeLabel(first), story.TimeLabel(second));
        Assert.NotEqual(story.Fraction(first), story.Fraction(second));
        Assert.Contains("+02:00", story.TimeLabel(first));
        Assert.Contains("+01:00", story.TimeLabel(second));
    }

    [Fact]
    public void IntervalsAreClippedAndMissingTracksStayUnknown()
    {
        var empty = Day();
        var input = new List<StoryInterval>
        {
            new("boundary", "garden", StoryKind.Contact, empty.Start.AddHours(-1), empty.Start.AddMinutes(20),
                "Gate open", StoryQuality.Recorded, "fixture-gate")
        };
        var story = Day(intervals: input);
        input.Clear();
        Assert.Equal(story.Start, Assert.Single(story.Intervals).Start);
        Assert.Empty(story.At("garden", StoryKind.Lighting, story.Start));
        Assert.Empty(story.At("garden", StoryKind.Contact, story.Start.AddMinutes(20)));
        Assert.Single(story.At("garden", StoryKind.Contact, story.Start));
    }

    [Fact]
    public void ChapterEvidenceCannotLeakOutsideSelectedInterval()
    {
        var empty = Day();
        var interval = new StoryInterval("light", "living", StoryKind.Lighting, empty.Start.AddHours(6), empty.Start.AddHours(8),
            "Light reported on", StoryQuality.Recorded, "fixture-light");
        var chapter = new StoryChapter("morning", "A light in the living room",
            empty.Start.AddHours(7), empty.Start.AddHours(7.5), ["light"]);
        var story = Day(intervals: [interval], chapters: [chapter]);
        var evidence = Assert.Single(story.EvidenceFor("morning"));
        Assert.Equal(chapter.Start, evidence.Start);
        Assert.Equal(chapter.End, evidence.End);
        Assert.Equal("fixture-light", evidence.SourceId);
        Assert.Throws<ArgumentException>(() => Day(intervals: [interval], chapters:
            [chapter with { Start = empty.Start.AddHours(9), End = empty.Start.AddHours(10) }]));
    }

    [Fact]
    public void RejectsCrossPlaceOverlappingAndInvalidHistory()
    {
        var empty = Day();
        var interval = new StoryInterval("light", "living", StoryKind.Lighting, empty.Start.AddHours(6), empty.Start.AddHours(8),
            "Light reported on", StoryQuality.Recorded, "fixture-light");
        Assert.Throws<ArgumentException>(() => Day(intervals: [interval with { PlaceId = "another-home-room" }]));
        Assert.Throws<ArgumentException>(() => Day(intervals: [interval, interval with { Id = "overlap" }]));
        Assert.Throws<ArgumentException>(() => Day(intervals: [interval with { End = interval.Start }]));
        Assert.Throws<ArgumentException>(() => Day(intervals: [interval with { SourceId = "" }]));
    }

    private static StoryDay Day(DateOnly? date = null, IEnumerable<StoryInterval>? intervals = null, IEnumerable<StoryChapter>? chapters = null) =>
        new(SampleHome.Create(), date ?? new DateOnly(2026, 10, 23), TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich"),
            intervals ?? [], chapters ?? []);
}

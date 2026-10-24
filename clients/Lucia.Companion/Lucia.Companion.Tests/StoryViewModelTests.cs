using Lucia.Companion.App;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class StoryViewModelTests
{
    [Fact]
    public void ChaptersAndScrubbingUseOneAuthoritativeInstant()
    {
        var state = new StoryViewModel(SampleStory.Create(SampleHome.Create()));
        var chapter = state.VisibleChapters.Last();
        state.SelectChapter(chapter.Id);
        Assert.Equal(chapter.Start, state.SelectedInstant);
        Assert.Equal(chapter.Id, state.SelectedChapterId);
        Assert.Equal(state.Day.Fraction(chapter.Start), state.CursorFraction);
        state.SelectFraction(.5);
        Assert.Equal(state.Day.InstantAt(.5), state.SelectedInstant);
        Assert.Null(state.SelectedChapterId);
    }

    [Fact]
    public void PlaceFilterRestrictsChaptersButDoesNotInventMissingHistory()
    {
        var state = new StoryViewModel(SampleStory.Create(SampleHome.Create()));
        state.SelectPlace("bedroom");
        Assert.All(state.VisibleIntervals, interval => Assert.Equal("bedroom", interval.PlaceId));
        Assert.Empty(state.VisibleIntervals);
        Assert.Empty(state.VisibleChapters);
        Assert.Throws<ArgumentException>(() => state.SelectPlace("unknown-room"));
        state.SelectPlace(null);
        Assert.NotEmpty(state.VisibleChapters);
    }

    [Fact]
    public void TimelineEvidenceSelectionChangesPlaceAndTimeTogether()
    {
        var state = new StoryViewModel(SampleStory.Create(SampleHome.Create()));
        var interval = state.Day.Intervals.First();
        state.SelectInterval(interval.Id);
        Assert.Equal(interval.PlaceId, state.SelectedPlaceId);
        Assert.Equal(interval.Start, state.SelectedInstant);
        Assert.Null(state.SelectedChapterId);
    }
}

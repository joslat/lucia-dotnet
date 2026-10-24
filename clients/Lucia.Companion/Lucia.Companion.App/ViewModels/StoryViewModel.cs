using System.ComponentModel;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.ViewModels;

/// <summary>One selection for the orbit, chapters, filters and straight timelines.</summary>
public sealed class StoryViewModel : INotifyPropertyChanged
{
    public StoryViewModel(StoryDay day)
    {
        Day = day;
        SelectedInstant = day.Intervals.FirstOrDefault(interval => interval.Quality == StoryQuality.Recorded)?.Start ?? day.Start;
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public StoryDay Day { get; }
    public DateTimeOffset SelectedInstant { get; private set; }
    public string? SelectedPlaceId { get; private set; }
    public string? SelectedChapterId { get; private set; }
    public double CursorFraction => Day.Fraction(SelectedInstant);
    public IReadOnlyList<StoryInterval> VisibleIntervals => Day.Intervals.Where(interval => SelectedPlaceId is null || interval.PlaceId == SelectedPlaceId).ToArray();
    public IReadOnlyList<StoryChapter> VisibleChapters => Day.Chapters.Where(chapter =>
        SelectedPlaceId is null || Day.EvidenceFor(chapter.Id).Any(interval => interval.PlaceId == SelectedPlaceId)).ToArray();
    public IReadOnlyList<PlaceSetting> VisiblePlaces => Day.Places.Where(place => SelectedPlaceId is null || place.Id == SelectedPlaceId).ToArray();

    public void SelectFraction(double fraction)
    {
        SelectedInstant = Day.InstantAt(fraction);
        SelectedChapterId = null;
        Notify();
    }

    public void SelectChapter(string id)
    {
        var chapter = VisibleChapters.SingleOrDefault(chapter => chapter.Id == id) ??
            throw new ArgumentException("Choose a chapter available in this place filter.", nameof(id));
        SelectedChapterId = chapter.Id;
        SelectedInstant = chapter.Start;
        Notify();
    }

    public void SelectPlace(string? id)
    {
        if (id is not null && !Day.Places.Any(place => place.Id == id)) { throw new ArgumentException("Unknown place.", nameof(id)); }
        SelectedPlaceId = id;
        if (!VisibleChapters.Any(chapter => chapter.Id == SelectedChapterId)) { SelectedChapterId = null; }
        Notify();
    }

    public void SelectInterval(string id)
    {
        var interval = Day.Intervals.SingleOrDefault(interval => interval.Id == id) ??
            throw new ArgumentException("Unknown history interval.", nameof(id));
        SelectedPlaceId = interval.PlaceId;
        SelectedInstant = interval.Start;
        SelectedChapterId = null;
        Notify();
    }

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}

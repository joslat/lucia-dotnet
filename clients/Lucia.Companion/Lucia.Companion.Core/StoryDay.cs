using System.Globalization;

namespace Lucia.Companion.Core;

/// <summary>One home calendar day. All drawings and selection map the same elapsed UTC interval.</summary>
public sealed class StoryDay
{
    public StoryDay(HomeTopology home, DateOnly date, TimeZoneInfo timeZone,
        IEnumerable<StoryInterval> intervals, IEnumerable<StoryChapter> chapters)
    {
        ArgumentNullException.ThrowIfNull(home);
        ArgumentNullException.ThrowIfNull(timeZone);
        ArgumentNullException.ThrowIfNull(intervals);
        ArgumentNullException.ThrowIfNull(chapters);
        HomeId = home.Id;
        Date = date;
        TimeZone = timeZone;
        Start = Boundary(date, timeZone);
        End = Boundary(date.AddDays(1), timeZone);
        Places = Array.AsReadOnly(home.Places.Select(place => new PlaceSetting(place.Id, place.Name, place.Icon)).ToArray());
        var placeIds = Places.Select(place => place.Id).ToHashSet(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var normalized = new List<StoryInterval>();
        foreach (var interval in intervals)
        {
            ArgumentNullException.ThrowIfNull(interval);
            if (string.IsNullOrWhiteSpace(interval.Id) || !ids.Add(interval.Id) || !placeIds.Contains(interval.PlaceId) ||
                interval.End <= interval.Start || string.IsNullOrWhiteSpace(interval.Label) ||
                string.IsNullOrWhiteSpace(interval.SourceId) || !Enum.IsDefined(interval.Kind) || !Enum.IsDefined(interval.Quality))
            {
                throw new ArgumentException("History needs unique IDs, a known place, a positive interval, a label, quality and source.");
            }
            if (interval.Start >= End || interval.End <= Start) { continue; }
            normalized.Add(interval with { Start = interval.Start < Start ? Start : interval.Start, End = interval.End > End ? End : interval.End });
        }
        foreach (var track in normalized.GroupBy(interval => (interval.PlaceId, interval.Kind)))
        {
            var ordered = track.OrderBy(interval => interval.Start).ToArray();
            if (ordered.Zip(ordered.Skip(1)).Any(pair => pair.First.End > pair.Second.Start))
            {
                throw new ArgumentException("Normalized intervals cannot overlap within one place and observation track.");
            }
        }
        Intervals = Array.AsReadOnly(normalized.OrderBy(interval => interval.Start).ThenBy(interval => interval.Id, StringComparer.Ordinal).ToArray());
        var chapterIds = new HashSet<string>(StringComparer.Ordinal);
        Chapters = Array.AsReadOnly(chapters.Select(chapter =>
        {
            ArgumentNullException.ThrowIfNull(chapter);
            if (string.IsNullOrWhiteSpace(chapter.Id) || !chapterIds.Add(chapter.Id) || string.IsNullOrWhiteSpace(chapter.Title) ||
                chapter.Start < Start || chapter.End > End || chapter.End <= chapter.Start ||
                chapter.EvidenceIds is null || chapter.EvidenceIds.Count == 0 ||
                chapter.EvidenceIds.Distinct(StringComparer.Ordinal).Count() != chapter.EvidenceIds.Count ||
                chapter.EvidenceIds.Any(id => !Intervals.Any(interval => interval.Id == id && interval.Quality == StoryQuality.Recorded &&
                    interval.Start < chapter.End && interval.End > chapter.Start)))
            {
                throw new ArgumentException("A chapter must reference recorded evidence that intersects its interval in this day.");
            }
            return chapter with { EvidenceIds = Array.AsReadOnly(chapter.EvidenceIds.ToArray()) };
        }).OrderBy(chapter => chapter.Start).ToArray());
    }

    public string HomeId { get; }
    public DateOnly Date { get; }
    public TimeZoneInfo TimeZone { get; }
    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }
    public IReadOnlyList<PlaceSetting> Places { get; }
    public IReadOnlyList<StoryInterval> Intervals { get; }
    public IReadOnlyList<StoryChapter> Chapters { get; }

    public double Fraction(DateTimeOffset instant) => Math.Clamp((instant - Start).TotalSeconds / (End - Start).TotalSeconds, 0, 1);

    public DateTimeOffset InstantAt(double fraction)
    {
        if (!double.IsFinite(fraction)) { throw new ArgumentOutOfRangeException(nameof(fraction)); }
        var ticks = Math.Min((End - Start).Ticks - 1, (long)((End - Start).Ticks * Math.Clamp(fraction, 0, 1)));
        return Start.AddTicks(ticks);
    }

    public string TimeLabel(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, TimeZone).ToString("HH:mm zzz", CultureInfo.InvariantCulture);

    public IReadOnlyList<StoryInterval> At(string placeId, StoryKind kind, DateTimeOffset instant) =>
        Intervals.Where(interval => interval.PlaceId == placeId && interval.Kind == kind && interval.Start <= instant && instant < interval.End).ToArray();

    public IReadOnlyList<StoryInterval> EvidenceFor(string chapterId)
    {
        var chapter = Chapters.Single(chapter => chapter.Id == chapterId);
        return Intervals.Where(interval => chapter.EvidenceIds.Contains(interval.Id, StringComparer.Ordinal))
            .Select(interval => interval with { Start = interval.Start < chapter.Start ? chapter.Start : interval.Start,
                End = interval.End > chapter.End ? chapter.End : interval.End }).ToArray();
    }

    private static DateTimeOffset Boundary(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        // Some zones change at midnight. Advance through a skipped midnight, but reject an entirely skipped date.
        while (zone.IsInvalidTime(local) && DateOnly.FromDateTime(local) == date) { local = local.AddMinutes(1); }
        if (DateOnly.FromDateTime(local) != date) { throw new ArgumentException("This calendar date does not exist in the home's timezone."); }
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}

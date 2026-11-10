using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;

namespace Lucia.Companion.App;

/// <summary>
/// Deterministic synthetic reports. No presence estimation, models, network or household data.
/// The sample week runs from Sunday 18 to Saturday 24 October 2026. Friday 23 is the original authored day; the other days
/// shift its reports by a fixed number of minutes and leave a few out. Today, the 24th, stops at the sample clock.
/// </summary>
public static class SampleStory
{
    private static readonly int[] ShiftMinutes = [-20, 15, 5, -10, 25, 0, 10];
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    public static IReadOnlyList<DateOnly> Dates { get; } = Array.AsReadOnly(Enumerable.Range(0, 7).Select(day => new DateOnly(2026, 10, 18).AddDays(day)).ToArray());
    public static DateOnly Today => Dates[^1];

    /// <summary>The original authored day, Friday 23 October.</summary>
    public static StoryDay Create(HomeTopology home) => Day(home, new DateOnly(2026, 10, 23));

    public static StoryNavigator Navigator(HomeTopology home) => new(Dates, Today, date => Day(home, date))
    {
        AttentionCount = home.Places.Count(HomeAttention.NeedsYou),
    };

    public static StoryDay Day(HomeTopology home, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(home);
        var index = Dates.ToList().IndexOf(date);
        if (index < 0) { throw new ArgumentOutOfRangeException(nameof(date), "The sample week runs from 18 to 24 October 2026."); }
        var shift = TimeSpan.FromMinutes(ShiftMinutes[index]);
        var now = date == Today ? SampleHome.CapturedAt : DateTimeOffset.MaxValue;
        DateTimeOffset At(int hour, int minute) => new DateTimeOffset(date.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(2)) + shift;
        StoryInterval[] examples =
        [
            new("morning-light", "living", StoryKind.Lighting, At(6, 30), At(7, 45), "Light reported on", StoryQuality.Recorded, "fixture-living-light"),
            new("kitchen-motion", "kitchen", StoryKind.Motion, At(6, 50), At(6, 51), "Motion reported in this minute", StoryQuality.Recorded, "fixture-kitchen-motion"),
            new("weather-request", "kitchen", StoryKind.Lucia, At(6, 52), At(6, 53), "Weather request completed", StoryQuality.Recorded, "fixture-request-weather"),
            new("porch-contact", "porch", StoryKind.Contact, At(7, 25), At(7, 27), "Door reported open", StoryQuality.Recorded, "fixture-porch-contact"),
            new("garden-contact", "garden", StoryKind.Contact, At(12, 10), At(12, 16), "Gate reported open", StoryQuality.Recorded, "fixture-garden-contact"),
            new("porch-light", "porch", StoryKind.Lighting, At(18, 10), At(21, 30), "Light reported on", StoryQuality.Recorded, "fixture-porch-light"),
            new("evening-light", "living", StoryKind.Lighting, At(18, 15), At(22, 10), "Light reported on", StoryQuality.Recorded, "fixture-living-light"),
            new("garden-last-known", "garden", StoryKind.Contact, At(19, 0), At(20, 0), "Last-known gate closed, not a current report", StoryQuality.LastKnown, "fixture-garden-contact"),
            new("garden-gap", "garden", StoryKind.Contact, At(20, 0), At(23, 59), "Contact source unavailable", StoryQuality.Unavailable, "fixture-garden-contact")
        ];
        // A few authored absences so the days differ: no front door report on Sunday, no garden gate on Wednesday.
        string[] absent = date.Day switch { 18 => ["porch-contact"], 21 => ["garden-contact"], _ => [] };
        var intervals = examples
            .Where(interval => home.Places.Any(place => place.Id == interval.PlaceId) && !absent.Contains(interval.Id) && interval.Start < now)
            .Select(interval => interval.End > now ? interval with { End = now } : interval)
            .ToArray();
        StoryChapter[] chapters =
        [
            new("morning", "The morning starts", At(6, 30), At(7, 0), ["morning-light", "kitchen-motion", "weather-request"]),
            new("door", "A report at the front door", At(7, 25), At(7, 27), ["porch-contact"]),
            new("garden", "A moment in the garden", At(12, 10), At(12, 16), ["garden-contact"]),
            new("evening", "Lights into the evening", At(18, 10), At(22, 10), ["porch-light", "evening-light"])
        ];
        var availableChapters = chapters.Where(chapter => chapter.Start < now).Select(chapter => chapter with
        {
            End = chapter.End > now ? now : chapter.End,
            EvidenceIds = chapter.EvidenceIds.Where(id => intervals.Any(interval => interval.Id == id)).ToArray()
        }).Where(chapter => chapter.EvidenceIds.Count > 0);
        return new(home, date, Zone, intervals, availableChapters) { Now = date == Today ? SampleHome.CapturedAt : null };
    }
}

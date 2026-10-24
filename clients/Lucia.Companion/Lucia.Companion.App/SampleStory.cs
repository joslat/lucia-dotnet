using Lucia.Companion.Core;

namespace Lucia.Companion.App;

/// <summary>Deterministic synthetic reports. No presence estimation, models, network or household data.</summary>
public static class SampleStory
{
    public static StoryDay Create(HomeTopology home)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");
        var date = new DateOnly(2026, 10, 23);
        DateTimeOffset At(int hour, int minute) => new(date.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(2));
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
        var intervals = examples.Where(interval => home.Places.Any(place => place.Id == interval.PlaceId)).ToArray();
        StoryChapter[] chapters =
        [
            new("morning", "The morning starts", At(6, 30), At(7, 0), ["morning-light", "kitchen-motion", "weather-request"]),
            new("door", "A report at the front door", At(7, 25), At(7, 27), ["porch-contact"]),
            new("garden", "A moment in the garden", At(12, 10), At(12, 16), ["garden-contact"]),
            new("evening", "Lights into the evening", At(18, 10), At(22, 10), ["porch-light", "evening-light"])
        ];
        var availableChapters = chapters.Select(chapter => chapter with
        {
            EvidenceIds = chapter.EvidenceIds.Where(id => intervals.Any(interval => interval.Id == id)).ToArray()
        }).Where(chapter => chapter.EvidenceIds.Count > 0);
        return new(home, date, zone, intervals, availableChapters);
    }
}

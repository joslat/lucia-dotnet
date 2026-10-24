namespace Lucia.Companion.Core;

/// <summary>A normalized half-open reporting interval, not an occupancy inference.</summary>
public sealed record StoryInterval(string Id, string PlaceId, StoryKind Kind, DateTimeOffset Start, DateTimeOffset End,
    string Label, StoryQuality Quality, string SourceId);

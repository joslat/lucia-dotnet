namespace lucia.Agents.Companion;

/// <summary>Bounded source samples. Empty tracks do not assert inactivity, absence or retention coverage.</summary>
public sealed record CompanionHistorySnapshot(string HomeId, DateTimeOffset CapturedAt, DateTimeOffset Start,
    DateTimeOffset End, string TimeZone, IReadOnlyList<CompanionHistoryTrack> Tracks);

namespace lucia.Agents.Companion;

public sealed record CompanionHomeSnapshot(string HomeId, DateTimeOffset CapturedAt, double MaximumObservationAgeSeconds,
    IReadOnlyList<CompanionPlaceSnapshot> Places);

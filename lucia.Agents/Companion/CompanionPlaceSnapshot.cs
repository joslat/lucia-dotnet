namespace lucia.Agents.Companion;

public sealed record CompanionPlaceSnapshot(string PlaceId, string Name, IReadOnlyList<CompanionSource> Sources,
    IReadOnlyList<CompanionReading> Readings);

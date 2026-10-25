namespace lucia.Agents.Companion;

/// <summary>A recorder sample, not proof of physical state or continuous recorder coverage.</summary>
public sealed record CompanionHistorySample(DateTimeOffset At, bool IsBoundary, CompanionHistoryQuality Quality,
    double? NumberValue, bool? ActiveValue, string? Unit);

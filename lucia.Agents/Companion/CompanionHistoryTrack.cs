namespace lucia.Agents.Companion;

/// <summary>CurrentPlaceId qualifies current access only. Historical room assignment requires effective-dated bindings.</summary>
public sealed record CompanionHistoryTrack(string SourceId, string CurrentPlaceId, CompanionField Field,
    IReadOnlyList<CompanionHistorySample> Samples);

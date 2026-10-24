namespace lucia.Agents.Companion;

/// <summary>Server-side lookup result. Entity selectors never enter the native activity projection.</summary>
public sealed record CompanionTargetAreaSnapshot(DateTimeOffset CheckedAt, IReadOnlyDictionary<string, CompanionTargetArea> AreasByEntity);

namespace lucia.Agents.Companion;

/// <summary>One qualified HA report. It is not a physical verification or inferred presence.</summary>
public sealed record CompanionReading(string SourceId, CompanionField Field, CompanionReadingQuality Quality,
    DateTimeOffset? ReportedAt, double? NumberValue, bool? IsActive, string? Unit);

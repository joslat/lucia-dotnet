namespace lucia.AgentHost.Companion;

/// <summary>Allowlisted request lifecycle only. No transcript, response, tool arguments, trace or inferred place.</summary>
public sealed record CompanionActivityItem(string RequestId, CompanionActivityState State,
    DateTimeOffset StartedAt, DateTimeOffset UpdatedAt, long Revision, string? PreviousRequestId = null,
    CompanionActivityCategory Category = CompanionActivityCategory.General, CompanionActivityTargetAreas? CurrentTargetAreas = null);

using lucia.Agents.Companion;

namespace lucia.AgentHost.Companion;

/// <summary>Current registry areas for reported skill inputs, not historical origin or verified effects.</summary>
public sealed record CompanionActivityTargetAreas(DateTimeOffset CheckedAt, bool IsPartial, IReadOnlyList<CompanionTargetArea> Places);

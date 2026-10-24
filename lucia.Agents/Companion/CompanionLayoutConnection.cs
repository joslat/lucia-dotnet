namespace lucia.Agents.Companion;

/// <summary>Explicit physical adjacency, never inferred from activity or sensor bindings.</summary>
public sealed record CompanionLayoutConnection(string FromPlaceId, string ToPlaceId);

namespace lucia.Agents.Companion;

/// <summary>Owner storage result. Owner identity and the whole-house revision are not member projections.</summary>
public sealed record CompanionLayoutSnapshot(string Revision, CompanionLayout Layout, DateTimeOffset UpdatedAt, string UpdatedBy);

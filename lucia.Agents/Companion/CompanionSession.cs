namespace lucia.Agents.Companion;

public sealed record CompanionSession(string Id, string DeviceName, CompanionGrant Grant, DateTimeOffset ExpiresAt);

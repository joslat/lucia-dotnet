namespace lucia.Agents.Companion;

public sealed record CompanionPairingReview(string Id, string HomeId, string DeviceName, DateTimeOffset ExpiresAt, string Status);

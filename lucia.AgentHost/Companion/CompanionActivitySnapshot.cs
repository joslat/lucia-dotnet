namespace lucia.AgentHost.Companion;

/// <summary>Bounded attributed activity since collection started in this process, not a complete household history.</summary>
public sealed record CompanionActivitySnapshot(string HomeId, string MemberId, DateTimeOffset CapturedAt,
    DateTimeOffset CoverageStartedAt, bool IsConfigured, bool HasGap, bool HasMore, IReadOnlyList<CompanionActivityItem> Requests);

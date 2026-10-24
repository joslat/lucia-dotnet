namespace lucia.AgentHost.Companion;

// Correlation stays server-side and shares the same bounded lifetime as the projected request.
internal sealed record CompanionActivityEntry(CompanionActivityItem Item, string? ConversationKey, bool HasLaterTurn = false);

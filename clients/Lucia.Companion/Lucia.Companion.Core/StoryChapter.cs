namespace Lucia.Companion.Core;

/// <summary>An evidence-linked interval. Fixture chapters are authored, not a production narrative engine.</summary>
public sealed record StoryChapter(string Id, string Title, DateTimeOffset Start, DateTimeOffset End,
    IReadOnlyList<string> EvidenceIds);

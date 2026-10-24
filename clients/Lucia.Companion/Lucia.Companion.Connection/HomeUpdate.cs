namespace Lucia.Companion.Connection;

/// <summary>An authorized full replacement, or confirmation of the caller's in-memory revision. Never persisted.</summary>
public sealed record HomeUpdate
{
    public string Revision { get; }
    public HomeSnapshot? Snapshot { get; }
    public HomeUpdate(string revision, HomeSnapshot? snapshot)
    {
        if (!IsRevision(revision)) { throw new ArgumentException("A bounded Home revision is required.", nameof(revision)); }
        Revision = revision; Snapshot = snapshot;
    }

    internal static bool IsRevision(string? revision)
    {
        if (revision is not { Length: 68 } || !revision.StartsWith("W/\"", StringComparison.Ordinal) || revision[^1] != '"') { return false; }
        foreach (var character in revision.AsSpan(3, 64))
        { if (character is not (>= '0' and <= '9' or >= 'A' and <= 'F')) { return false; } }
        return true;
    }
}

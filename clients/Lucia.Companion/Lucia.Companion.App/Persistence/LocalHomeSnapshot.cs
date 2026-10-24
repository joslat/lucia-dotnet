using Lucia.Companion.Core;

namespace Lucia.Companion.App.Persistence;

/// <summary>The exact saved bytes' revision, including an unreadable file. Null revision means absent.</summary>
public sealed record LocalHomeSnapshot(string? Revision, HomeProfile? Profile)
{
    public bool IsUnreadable => Revision is not null && Profile is null;
}

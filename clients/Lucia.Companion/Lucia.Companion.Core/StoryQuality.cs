namespace Lucia.Companion.Core;

/// <summary>Historical evidence quality. An absent interval remains unknown, not an off/closed/unoccupied state.</summary>
public enum StoryQuality
{
    Recorded,
    LastKnown,
    Unavailable
}

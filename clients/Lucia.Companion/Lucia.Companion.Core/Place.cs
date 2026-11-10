namespace Lucia.Companion.Core;

/// <summary>Only the information needed by the first read-only place control.</summary>
public sealed record Place(string Id, string Name, PlaceState State, string Reason, IReadOnlyList<Observation> Observations)
{
    public PlaceIcon Icon { get; init; } = PlaceIcon.Home;
    /// <summary>Optional local zone used to group places on Home. Null means the household hasn't set one.</summary>
    public PlaceZone? Zone { get; init; }
}

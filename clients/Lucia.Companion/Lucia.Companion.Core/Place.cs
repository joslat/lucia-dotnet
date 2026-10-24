namespace Lucia.Companion.Core;

/// <summary>Only the information needed by the first read-only place control.</summary>
public sealed record Place(string Id, string Name, PlaceState State, string Reason, IReadOnlyList<Observation> Observations)
{
    public PlaceIcon Icon { get; init; } = PlaceIcon.Home;
}

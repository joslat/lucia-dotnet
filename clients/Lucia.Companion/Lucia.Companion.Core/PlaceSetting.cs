namespace Lucia.Companion.Core;

/// <summary>Local configuration only. Observations never belong in this record.</summary>
public sealed record PlaceSetting(string Id, string Name, PlaceIcon Icon)
{
    /// <summary>Optional; older saved setups have none.</summary>
    public PlaceZone? Zone { get; init; }
}

namespace Lucia.Companion.Core;

/// <summary>Local configuration only. Observations never belong in this record.</summary>
public sealed record PlaceSetting(string Id, string Name, PlaceIcon Icon);

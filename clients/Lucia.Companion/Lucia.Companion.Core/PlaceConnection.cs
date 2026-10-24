namespace Lucia.Companion.Core;

/// <summary>Undirected physical adjacency. It conveys no sensor ownership or authority.</summary>
public sealed record PlaceConnection(string FromPlaceId, string ToPlaceId);

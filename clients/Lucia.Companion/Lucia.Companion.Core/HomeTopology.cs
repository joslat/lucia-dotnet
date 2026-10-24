namespace Lucia.Companion.Core;

/// <summary>A validated, immutable home read model. Layout and physical adjacency remain separate.</summary>
public sealed class HomeTopology
{
    public HomeTopology(string id, string name, IEnumerable<Place> places, IEnumerable<PlaceConnection> connections)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(connections);
        var snapshot = places.Select(place =>
        {
            ArgumentNullException.ThrowIfNull(place);
            ArgumentException.ThrowIfNullOrWhiteSpace(place.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(place.Name);
            ArgumentNullException.ThrowIfNull(place.Observations);
            if (!Enum.IsDefined(place.State) || !Enum.IsDefined(place.Icon))
            {
                throw new ArgumentException("A place has an unsupported state or icon.", nameof(places));
            }

            return place with { Observations = Array.AsReadOnly(place.Observations.ToArray()) };
        }).ToArray();
        var ids = snapshot.Select(place => place.Id).ToHashSet(StringComparer.Ordinal);
        if (ids.Count != snapshot.Length)
        {
            throw new ArgumentException("Place identifiers must be unique within a home.", nameof(places));
        }

        var edges = connections.ToArray();
        var unique = new HashSet<(string, string)>();
        foreach (var edge in edges)
        {
            ArgumentNullException.ThrowIfNull(edge);
            if (!ids.Contains(edge.FromPlaceId) || !ids.Contains(edge.ToPlaceId) || edge.FromPlaceId == edge.ToPlaceId)
            {
                throw new ArgumentException("Connections must join two different places in this home.", nameof(connections));
            }

            var key = StringComparer.Ordinal.Compare(edge.FromPlaceId, edge.ToPlaceId) < 0
                ? (edge.FromPlaceId, edge.ToPlaceId) : (edge.ToPlaceId, edge.FromPlaceId);
            if (!unique.Add(key))
            {
                throw new ArgumentException("A connection may appear only once, in either direction.", nameof(connections));
            }
        }

        Id = id;
        Name = name;
        Places = Array.AsReadOnly(snapshot);
        Connections = Array.AsReadOnly(edges);
    }

    public string Id { get; }
    public string Name { get; }
    public IReadOnlyList<Place> Places { get; }
    public IReadOnlyList<PlaceConnection> Connections { get; }
}

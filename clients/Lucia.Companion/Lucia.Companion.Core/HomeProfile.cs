namespace Lucia.Companion.Core;

/// <summary>Versioned local setup, deliberately excluding sensor values and task history.</summary>
public sealed record HomeProfile(int SchemaVersion, string HomeId, string HomeName,
    IReadOnlyList<PlaceSetting> Places, IReadOnlyList<PlaceConnection> Connections, IReadOnlyList<string> PinnedPlaceIds)
{
    public static HomeProfile From(HomeTopology home, IReadOnlyList<string> pinnedIds) => new(1, home.Id, home.Name,
        home.Places.Select(place => new PlaceSetting(place.Id, place.Name, place.Icon)).ToArray(), home.Connections.ToArray(), pinnedIds.ToArray());

    public void Validate()
    {
        if (SchemaVersion != 1) { throw new ArgumentException("This setup version is not supported."); }
        ArgumentNullException.ThrowIfNull(Places);
        ArgumentNullException.ThrowIfNull(Connections);
        ArgumentNullException.ThrowIfNull(PinnedPlaceIds);
        if (Places.Count > 200) { throw new ArgumentException("This prototype supports at most 200 places."); }
        var home = BuildTopology([]);
        if (HomeName.Length > 80 || Places.Any(place => place.Name.Length > 80 || place.Id.Length > 128))
        {
            throw new ArgumentException("A saved name or identifier is too long.");
        }

        if (PinnedPlaceIds.Distinct(StringComparer.Ordinal).Count() != PinnedPlaceIds.Count ||
            PinnedPlaceIds.Any(id => !home.Places.Any(place => place.Id == id)))
        {
            throw new ArgumentException("Pins must refer to unique places in this home.");
        }
    }

    public HomeTopology ApplyTo(HomeTopology observedHome)
    {
        Validate();
        if (HomeId != observedHome.Id) { throw new ArgumentException("This setup belongs to a different home."); }
        return BuildTopology(observedHome.Places);
    }

    private HomeTopology BuildTopology(IReadOnlyList<Place> observedPlaces) => new(HomeId, HomeName,
        Places.Select(setting =>
        {
            ArgumentNullException.ThrowIfNull(setting);
            var observed = observedPlaces.FirstOrDefault(place => place.Id == setting.Id);
            return observed is not null
                ? observed with { Name = setting.Name, Icon = setting.Icon }
                : new Place(setting.Id, setting.Name, PlaceState.Unavailable, "No observation bindings configured.", []) { Icon = setting.Icon };
        }), Connections);
}

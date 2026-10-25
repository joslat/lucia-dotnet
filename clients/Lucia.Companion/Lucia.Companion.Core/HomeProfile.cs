namespace Lucia.Companion.Core;

/// <summary>Versioned local setup, deliberately excluding sensor values and task history.</summary>
public sealed record HomeProfile(int SchemaVersion, string HomeId, string HomeName,
    IReadOnlyList<PlaceSetting> Places, IReadOnlyList<PlaceConnection> Connections, IReadOnlyList<string> PinnedPlaceIds)
{
    public const int MaximumNameLength = 200;
    public IReadOnlyList<HomeSection>? SectionOrder { get; init; }
    public IReadOnlyList<ObservationBinding>? Bindings { get; init; }

    public static HomeProfile From(HomeTopology home, IReadOnlyList<string> pinnedIds, IReadOnlyList<HomeSection>? sectionOrder = null) => new(1, home.Id, home.Name,
        home.Places.Select(place => new PlaceSetting(place.Id, place.Name, place.Icon)).ToArray(), home.Connections.ToArray(), pinnedIds.ToArray())
        { SectionOrder = sectionOrder is null ? null : HomeSections.ValidateOrder(sectionOrder), Bindings = home.Bindings.ToArray() };

    public void Validate()
    {
        if (SchemaVersion != 1) { throw new ArgumentException("This setup version is not supported."); }
        ArgumentNullException.ThrowIfNull(Places);
        ArgumentNullException.ThrowIfNull(Connections);
        ArgumentNullException.ThrowIfNull(PinnedPlaceIds);
        if (SectionOrder is not null) { HomeSections.ValidateOrder(SectionOrder); }
        if (Places.Count > 200) { throw new ArgumentException("This prototype supports at most 200 places."); }
        var home = BuildTopology([]);
        if (HomeName.Length > MaximumNameLength || Places.Any(place => place.Name.Length > MaximumNameLength || place.Id.Length > 128))
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
                : new Place(setting.Id, setting.Name, PlaceState.Unavailable,
                    ObservationBinding.MissingReadingsReason(Bindings?.Any(binding => binding.PlaceId == setting.Id) == true), []) { Icon = setting.Icon };
        }), Connections, Bindings);
}

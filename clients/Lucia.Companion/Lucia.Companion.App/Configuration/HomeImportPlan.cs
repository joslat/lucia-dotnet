using Lucia.Companion.Connection;
using Lucia.Companion.Core;
using System.Globalization;

namespace Lucia.Companion.App.Configuration;

public sealed class HomeImportPlan
{
    private readonly HomeTopology baseline;
    private HomeImportPlan(HomeTopology baseline, HomeTopology preview, string[] places, ObservationBinding[] bindings, HomeImportConflict[] conflicts)
    {
        this.baseline = baseline; Preview = preview;
        AddedPlaceIds = Array.AsReadOnly(places); AddedBindings = Array.AsReadOnly(bindings); Conflicts = Array.AsReadOnly(conflicts);
    }
    public HomeTopology Preview { get; }
    public IReadOnlyList<string> AddedPlaceIds { get; }
    public IReadOnlyList<ObservationBinding> AddedBindings { get; }
    public IReadOnlyList<HomeImportConflict> Conflicts { get; }

    /// <summary>Stages metadata only. It neither saves the draft nor acquires household access.</summary>
    public static HomeImportPlan Create(HomeTopology original, HomeCatalog catalog, IReadOnlyList<HomeImportChoice> choices)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(choices);
        if (original.Id != catalog.HomeId || choices.Count > 200 || choices.Any(choice => choice is null)
            || choices.Select(choice => choice.PlaceId).Distinct(StringComparer.Ordinal).Count() != choices.Count)
        { throw new ArgumentException("Choose unique catalog places in this home."); }
        var available = catalog.Places.ToDictionary(place => place.PlaceId, StringComparer.Ordinal);
        var places = original.Places.ToList();
        var addedPlaces = new List<string>();
        var bindings = new List<ObservationBinding>();
        var conflicts = new List<HomeImportConflict>();
        var assigned = original.Bindings.ToDictionary(binding => (binding.SourceId, binding.Role));
        foreach (var choice in choices)
        {
            if (string.IsNullOrEmpty(choice.PlaceId) || !available.TryGetValue(choice.PlaceId, out var imported))
            { throw new ArgumentException("Choose a place returned by authorized discovery."); }
            var name = Name(choice.Name);
            if (!Enum.IsDefined(choice.Icon)) { throw new ArgumentException("Choose a supported place icon."); }
            if (!places.Any(place => place.Id == choice.PlaceId))
            {
                places.Add(new(choice.PlaceId, name, PlaceState.Unavailable, ObservationBinding.MissingReadingsReason(false), []) { Icon = choice.Icon });
                addedPlaces.Add(choice.PlaceId);
            }
            foreach (var source in imported.Sources)
            {
                var proposed = new ObservationBinding(choice.PlaceId, source.SourceId, source.Field, imported.PlaceId);
                if (assigned.TryGetValue((source.SourceId, source.Field), out var existing))
                {
                    if (existing.PlaceId != choice.PlaceId) { conflicts.Add(new(source.SourceId, source.Field, existing.PlaceId, choice.PlaceId)); }
                    continue;
                }
                assigned.Add((source.SourceId, source.Field), proposed);
                bindings.Add(proposed);
            }
        }
        var preview = original;
        if (addedPlaces.Count > 0 || bindings.Count > 0)
        {
            var combined = original.Bindings.Concat(bindings).ToArray();
            preview = new(original.Id, original.Name, places.Select(place => place.State == PlaceState.Unavailable && place.Observations.Count == 0
                ? place with { Reason = ObservationBinding.MissingReadingsReason(combined.Any(binding => binding.PlaceId == place.Id)) } : place), original.Connections, combined);
            HomeProfile.From(preview, []).Validate();
        }
        return new(original, preview, addedPlaces.ToArray(), bindings.ToArray(), conflicts.ToArray());
    }

    public HomeTopology ApplyTo(HomeTopology current) => ReferenceEquals(current, baseline) ? Preview
        : throw new InvalidOperationException("The draft changed after this import was reviewed. Review it again.");

    private static string Name(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var name = value.Trim();
        if (name.Length > 80 || name.Any(character => char.IsControl(character) || char.GetUnicodeCategory(character) == UnicodeCategory.Format))
        { throw new ArgumentException("Choose a readable place name of at most 80 characters."); }
        return name;
    }
}

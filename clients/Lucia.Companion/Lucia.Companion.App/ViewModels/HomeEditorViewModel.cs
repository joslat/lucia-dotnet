using Lucia.Companion.Core;

namespace Lucia.Companion.App.ViewModels;

/// <summary>A draft transaction. Cancel discards it; every topology mutation validates before replacing the draft.</summary>
public sealed class HomeEditorViewModel
{
    private HomeTopology draft;
    private readonly HomeTopology original;
    public HomeEditorViewModel(HomeTopology home) { draft = original = home; }
    public IReadOnlyList<Place> Places => draft.Places;
    public IReadOnlyList<PlaceConnection> Connections => draft.Connections;
    public IReadOnlyList<ObservationBinding> Bindings => draft.Bindings;
    public string HomeName => draft.Name;
    public bool HasChanges => draft.Name != original.Name ||
        !draft.Places.Select(place => (place.Id, place.Name, place.Icon)).SequenceEqual(original.Places.Select(place => (place.Id, place.Name, place.Icon))) ||
        !draft.Connections.Select(EdgeKey).ToHashSet().SetEquals(original.Connections.Select(EdgeKey)) ||
        !draft.Bindings.ToHashSet().SetEquals(original.Bindings);

    private static (string, string) EdgeKey(PlaceConnection edge) =>
        StringComparer.Ordinal.Compare(edge.FromPlaceId, edge.ToPlaceId) < 0
            ? (edge.FromPlaceId, edge.ToPlaceId) : (edge.ToPlaceId, edge.FromPlaceId);

    public string AddPlace(string name, PlaceIcon icon)
    {
        if (Places.Count >= 200) { throw new ArgumentException("This prototype supports at most 200 places."); }
        var id = Guid.NewGuid().ToString("N");
        var place = new Place(id, ValidateName(name), PlaceState.Unavailable, "No observation bindings configured.", []) { Icon = icon };
        Replace([.. Places, place], Connections);
        return id;
    }

    public void UpdatePlace(string id, string name, PlaceIcon icon)
    {
        RequirePlace(id);
        var validName = ValidateName(name);
        Replace(Places.Select(place => place.Id == id ? place with { Name = validName, Icon = icon } : place), Connections);
    }

    public void SetHomeName(string name) => draft = new(draft.Id, ValidateName(name), Places, Connections, Bindings);

    public void BindSource(string placeId, ObservationSource source, ObservationRole role)
    {
        RequirePlace(placeId);
        ArgumentNullException.ThrowIfNull(source);
        if (!source.SupportedRoles.Contains(role)) { throw new ArgumentException("Choose a role supported by this source.", nameof(role)); }
        var binding = new ObservationBinding(placeId, source.Id, role);
        if (Bindings.Contains(binding)) { return; }
        var assigned = Bindings.FirstOrDefault(existing => existing.SourceId == source.Id && existing.Role == role);
        if (assigned is not null)
        {
            var owner = Places.Single(place => place.Id == assigned.PlaceId).Name;
            throw new ArgumentException($"This source role is already bound to {owner}. Remove that binding before assigning it here.");
        }
        Replace(Places, Connections, Bindings.Append(binding));
    }

    public void UnbindSource(string placeId, string sourceId, ObservationRole role)
    {
        RequirePlace(placeId);
        var binding = new ObservationBinding(placeId, sourceId, role);
        Replace(Places, Connections, Bindings.Where(existing => existing != binding));
    }

    public void SetConnections(string id, IEnumerable<string> neighborIds)
    {
        RequirePlace(id);
        var unrelated = Connections.Where(edge => edge.FromPlaceId != id && edge.ToPlaceId != id);
        Replace(Places, unrelated.Concat(neighborIds.Select(neighbor => new PlaceConnection(id, neighbor))));
    }

    public void RemovePlace(string id)
    {
        RequirePlace(id);
        Replace(Places.Where(place => place.Id != id), Connections.Where(edge => edge.FromPlaceId != id && edge.ToPlaceId != id));
    }

    public void MovePlace(string id, int direction)
    {
        RequirePlace(id);
        if (direction is not (-1 or 1)) { throw new ArgumentOutOfRangeException(nameof(direction)); }
        var places = Places.ToList();
        var index = places.FindIndex(place => place.Id == id);
        var destination = Math.Clamp(index + direction, 0, places.Count - 1);
        (places[index], places[destination]) = (places[destination], places[index]);
        Replace(places, Connections);
    }

    public HomeTopology Build() => draft;
    private void Replace(IEnumerable<Place> places, IEnumerable<PlaceConnection> connections, IEnumerable<ObservationBinding>? bindings = null)
    {
        var snapshot = places.ToArray();
        var ids = snapshot.Select(place => place.Id).ToHashSet(StringComparer.Ordinal);
        var bound = (bindings ?? Bindings).Where(binding => ids.Contains(binding.PlaceId)).ToArray();
        var qualified = snapshot.Select(place => place.Observations.Count == 0 && place.State == PlaceState.Unavailable
            ? place with { Reason = ObservationBinding.MissingReadingsReason(bound.Any(binding => binding.PlaceId == place.Id)) } : place);
        draft = new(draft.Id, draft.Name, qualified, connections, bound);
    }
    private void RequirePlace(string id)
    {
        if (!Places.Any(place => place.Id == id)) { throw new ArgumentException("Choose a place in this home.", nameof(id)); }
    }

    private static string ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var value = name.Trim();
        if (value.Length > 80) { throw new ArgumentException("Keep the name to 80 characters or fewer.", nameof(name)); }
        return value;
    }
}

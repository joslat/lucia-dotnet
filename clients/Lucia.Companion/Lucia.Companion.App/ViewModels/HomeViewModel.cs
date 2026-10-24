using System.ComponentModel;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.ViewModels;

/// <summary>Shared screen state. No Avalonia, transport, storage or device actions.</summary>
public sealed class HomeViewModel(HomeTopology home, IEnumerable<ObservationSource>? sources = null, bool isDemo = true) : INotifyPropertyChanged
{
    private readonly HashSet<string> pinnedIds = new(StringComparer.Ordinal);

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? PreferencesChanged;
    public HomeTopology Home { get; private set; } = home ?? throw new ArgumentNullException(nameof(home));
    public IReadOnlyList<ObservationSource> SourceCatalog { get; private set; } = Array.AsReadOnly((sources ?? []).ToArray());
    public bool IsDemo { get; } = isDemo;
    public string SourceNotice { get; private set; } = isDemo ? "Synthetic readings · 24 Oct 2026 · 12:00 UTC" : "No verified Home reports available.";
    public Place? SelectedPlace { get; private set; }
    public bool IsActionsOpen { get; private set; }
    public bool IsDetailOpen { get; private set; }
    public IReadOnlyList<string> PinnedPlaceIds => Home.Places.Where(place => pinnedIds.Contains(place.Id)).Select(place => place.Id).ToArray();
    public IReadOnlyList<HomeSection> SectionOrder { get; private set; } = HomeSections.DefaultOrder;
    public IReadOnlyList<Place> AttentionPlaces => Home.Places
        .Where(place => place.State is PlaceState.Problem or PlaceState.Waiting or PlaceState.Unavailable)
        .OrderBy(place => place.State switch { PlaceState.Problem => 0, PlaceState.Waiting => 1, _ => 2 }).ToArray();

    public bool IsPinned(string placeId) => pinnedIds.Contains(placeId);

    public void SetSources(IEnumerable<ObservationSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        SourceCatalog = Array.AsReadOnly(sources.ToArray());
        Notify();
    }

    public void SetSourceNotice(string notice) { SourceNotice = notice; Notify(); }

    public void SetSectionOrder(IEnumerable<HomeSection> order, bool notifyPreferences = true)
    {
        var validated = HomeSections.ValidateOrder(order);
        if (validated.SequenceEqual(SectionOrder)) { return; }
        SectionOrder = validated;
        Notify();
        if (notifyPreferences) { PreferencesChanged?.Invoke(); }
    }

    public void SelectPlace(string id)
    {
        var place = Home.Places.FirstOrDefault(candidate => candidate.Id == id)
            ?? throw new ArgumentException("Select a place belonging to this home.", nameof(id));
        SelectedPlace = place;
        IsDetailOpen = true;
        IsActionsOpen = false;
        Notify();
    }

    public void OpenActions()
    {
        IsActionsOpen = SelectedPlace is not null;
        Notify();
    }

    public void CloseActions() { IsActionsOpen = false; Notify(); }
    public void CloseDetail() { IsDetailOpen = false; IsActionsOpen = false; Notify(); }
    public void ClearSelection() { SelectedPlace = null; CloseDetail(); }

    public void TogglePin()
    {
        if (SelectedPlace is not { } place)
        {
            return;
        }

        if (!pinnedIds.Add(place.Id))
        {
            pinnedIds.Remove(place.Id);
        }

        Notify();
        PreferencesChanged?.Invoke();
    }

    public void RestorePins(IEnumerable<string> ids)
    {
        var restored = ids.ToArray();
        if (restored.Any(id => !Home.Places.Any(place => place.Id == id))) { throw new ArgumentException("Pins must belong to this home.", nameof(ids)); }
        pinnedIds.Clear();
        pinnedIds.UnionWith(restored);
        Notify();
    }

    public void ReplaceHome(HomeTopology updatedHome, bool notifyPreferences = true)
    {
        ArgumentNullException.ThrowIfNull(updatedHome);
        if (updatedHome.Id != Home.Id)
        {
            throw new ArgumentException("A screen session cannot switch household identity.", nameof(updatedHome));
        }

        Home = updatedHome;
        pinnedIds.RemoveWhere(id => !Home.Places.Any(place => place.Id == id));
        SelectedPlace = Home.Places.FirstOrDefault(place => place.Id == SelectedPlace?.Id);
        IsDetailOpen &= SelectedPlace is not null;
        IsActionsOpen = false;
        Notify();
        if (notifyPreferences) { PreferencesChanged?.Invoke(); }
    }

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

public sealed partial class HomeConstellation : UserControl
{
    private readonly Dictionary<string, PlaceOrb> orbs = new(StringComparer.Ordinal);
    private readonly List<PlaceOrb> orderedOrbs = [];
    private readonly List<TextBlock> zoneLabels = [];
    /// <summary>Places in display order: grouped by zone when any zone is set, otherwise in setup order.</summary>
    public IReadOnlyList<PlaceOrb> Orbs => orderedOrbs;
    public IReadOnlyList<TextBlock> ZoneLabels => zoneLabels;
    public event Action<string>? PlaceInvoked;
    public event Action<string>? ActionsRequested;
    public event Action<string>? PinRequested;

    public HomeConstellation()
    {
        AvaloniaXamlLoader.Load(this);
        this.FindControl<ConstellationPanel>("Map")!.AnchorsChanged += this.FindControl<ConnectionLayer>("Links")!.SetAnchors;
    }

    public void SetHome(HomeTopology home)
    {
        var map = this.FindControl<ConstellationPanel>("Map")!;
        map.Children.Clear();
        orbs.Clear();
        orderedOrbs.Clear();
        zoneLabels.Clear();
        this.FindControl<TextBlock>("EmptyText")!.IsVisible = home.Places.Count == 0;
        foreach (var (zone, place) in DisplayOrder(home.Places))
        {
            if (zone is not null)
            {
                var label = new TextBlock { Text = zone, Classes = { "zone" } };
                zoneLabels.Add(label);
                map.Children.Add(label);
            }
            if (place is null) { continue; }
            var orb = new PlaceOrb(place, PlaceIcons.For(place.Icon)) { Tag = place.Id };
            orb.Classes.Add("constellation");
            orb.ShowSummary(PlaceCaption.Caption(place), PlaceCaption.Status(place), StatusIcon(place.State), place.Reason);
            orb.Click += (_, _) => PlaceInvoked?.Invoke(place.Id);
            var readings = new MenuItem { Header = "View readings" };
            readings.Click += (_, _) => PlaceInvoked?.Invoke(place.Id);
            var actions = new MenuItem { Header = "Place actions" };
            actions.Click += (_, _) => ActionsRequested?.Invoke(place.Id);
            var pin = new MenuItem { Header = "Toggle pin" };
            pin.Click += (_, _) => PinRequested?.Invoke(place.Id);
            orb.ContextMenu = new ContextMenu { ItemsSource = new[] { readings, actions, pin } };
            orbs.Add(place.Id, orb);
            orderedOrbs.Add(orb);
            map.Children.Add(orb);
        }

        this.FindControl<ConnectionLayer>("Links")!.SetConnections(home.Connections);
    }

    /// <summary>
    /// Yields a zone label before the first place of each zone, then that zone's places in setup order. Zones appear in the order
    /// their first place does. Places without a zone follow under "Other places" when other places have zones.
    /// </summary>
    private static IEnumerable<(string? Label, Place? Place)> DisplayOrder(IReadOnlyList<Place> places)
    {
        if (places.All(place => place.Zone is null))
        {
            foreach (var place in places) { yield return (null, place); }
            yield break;
        }
        foreach (var group in places.Where(place => place.Zone is not null).GroupBy(place => place.Zone))
        {
            yield return (PlaceZones.Label(group.Key), null);
            foreach (var place in group) { yield return (null, place); }
        }
        var other = places.Where(place => place.Zone is null).ToArray();
        if (other.Length == 0) { yield break; }
        yield return ("Other places", null);
        foreach (var place in other) { yield return (null, place); }
    }

    private static Avalonia.Media.Geometry? StatusIcon(PlaceState state) => state switch
    {
        PlaceState.Working => NavigationIcons.Lucia,
        PlaceState.Waiting => AttentionIcons.Choice,
        PlaceState.Problem => AttentionIcons.Fault,
        PlaceState.Unavailable => AttentionIcons.Silent,
        _ => null,
    };

    public void UpdateSelection(string? selectedId, Func<string, bool> isPinned)
    {
        foreach (var (id, orb) in orbs)
        {
            orb.IsSelected = id == selectedId;
            orb.IsPinned = isPinned(id);
            orb.SetValue(AutomationProperties.HelpTextProperty,
                $"{(orb.IsSelected ? "Selected. " : string.Empty)}{(orb.IsPinned ? "Pinned. " : string.Empty)}Activate for readings. Context menu has place actions.");
        }
    }

    public void FocusPlace(string? id)
    {
        if (id is not null && orbs.TryGetValue(id, out var orb)) { orb.Focus(); }
    }

    public Control? GetPlaceAnchor(string? id) => id is not null && orbs.TryGetValue(id, out var orb)
        ? orb.GetVisualDescendants().OfType<Control>().FirstOrDefault(control => control.Name == "Orb") ?? orb
        : null;
}

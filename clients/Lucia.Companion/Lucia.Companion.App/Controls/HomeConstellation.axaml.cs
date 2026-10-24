using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

public sealed partial class HomeConstellation : UserControl
{
    private readonly Dictionary<string, PlaceOrb> orbs = new(StringComparer.Ordinal);
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
        this.FindControl<TextBlock>("EmptyText")!.IsVisible = home.Places.Count == 0;
        foreach (var place in home.Places)
        {
            var orb = new PlaceOrb(place, PlaceIcons.For(place.Icon)) { Tag = place.Id };
            orb.Classes.Add("constellation");
            orb.Click += (_, _) => PlaceInvoked?.Invoke(place.Id);
            var readings = new MenuItem { Header = "View readings" };
            readings.Click += (_, _) => PlaceInvoked?.Invoke(place.Id);
            var actions = new MenuItem { Header = "Place actions" };
            actions.Click += (_, _) => ActionsRequested?.Invoke(place.Id);
            var pin = new MenuItem { Header = "Toggle pin" };
            pin.Click += (_, _) => PinRequested?.Invoke(place.Id);
            orb.ContextMenu = new ContextMenu { ItemsSource = new[] { readings, actions, pin } };
            orbs.Add(place.Id, orb);
            map.Children.Add(orb);
        }

        this.FindControl<ConnectionLayer>("Links")!.SetConnections(home.Connections);
    }

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

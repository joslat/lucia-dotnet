using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>A native button: touch, keyboard and automation all use the same activation.</summary>
public sealed class PlaceOrb : Button
{
    public static readonly StyledProperty<string> PlaceNameProperty = AvaloniaProperty.Register<PlaceOrb, string>(nameof(PlaceName), "Place");
    public static readonly StyledProperty<string> StateTextProperty = AvaloniaProperty.Register<PlaceOrb, string>(nameof(StateText), "Unknown");
    public static readonly StyledProperty<Geometry?> IconProperty = AvaloniaProperty.Register<PlaceOrb, Geometry?>(nameof(Icon));
    public static readonly StyledProperty<bool> IsSelectedProperty = AvaloniaProperty.Register<PlaceOrb, bool>(nameof(IsSelected));
    public static readonly StyledProperty<bool> IsPinnedProperty = AvaloniaProperty.Register<PlaceOrb, bool>(nameof(IsPinned));

    public string PlaceName { get => GetValue(PlaceNameProperty); set => SetValue(PlaceNameProperty, value); }
    public string StateText { get => GetValue(StateTextProperty); set => SetValue(StateTextProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public bool IsPinned { get => GetValue(IsPinnedProperty); set => SetValue(IsPinnedProperty, value); }

    static PlaceOrb()
    {
        IsSelectedProperty.Changed.AddClassHandler<PlaceOrb>((control, _) =>
        {
            control.PseudoClasses.Set(":selected", control.IsSelected);
            control.SetValue(AutomationProperties.HelpTextProperty, control.IsSelected ? "Selected. Shows read-only observations." : "Show read-only observations.");
        });
    }

    public PlaceOrb() { }

    public PlaceOrb(Place place, Geometry icon)
    {
        ArgumentNullException.ThrowIfNull(place);
        PlaceName = place.Name;
        StateText = place.State.ToString();
        Icon = icon;
        Classes.Add(place.State.ToString().ToLowerInvariant());
        SetValue(AutomationProperties.NameProperty, $"{place.Name}. {StateText}. {place.Reason}");
        ToolTip.SetTip(this, place.Reason);
    }
}

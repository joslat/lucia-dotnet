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
    public static readonly StyledProperty<string?> StatusTextProperty = AvaloniaProperty.Register<PlaceOrb, string?>(nameof(StatusText), "Unknown");
    public static readonly StyledProperty<string?> CaptionProperty = AvaloniaProperty.Register<PlaceOrb, string?>(nameof(Caption));
    public static readonly StyledProperty<Geometry?> StatusIconProperty = AvaloniaProperty.Register<PlaceOrb, Geometry?>(nameof(StatusIcon));
    public static readonly StyledProperty<Geometry?> IconProperty = AvaloniaProperty.Register<PlaceOrb, Geometry?>(nameof(Icon));
    public static readonly StyledProperty<bool> IsSelectedProperty = AvaloniaProperty.Register<PlaceOrb, bool>(nameof(IsSelected));
    public static readonly StyledProperty<bool> IsPinnedProperty = AvaloniaProperty.Register<PlaceOrb, bool>(nameof(IsPinned));

    public string PlaceName { get => GetValue(PlaceNameProperty); set => SetValue(PlaceNameProperty, value); }
    /// <summary>The written state. Null hides the line, for a quiet place whose caption already says what is going on.</summary>
    public string? StatusText { get => GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }
    /// <summary>A summary of current readings, such as "Lights off · 22.0 °C". Null when there is none.</summary>
    public string? Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public Geometry? StatusIcon { get => GetValue(StatusIconProperty); set => SetValue(StatusIconProperty, value); }
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
        StatusText = place.State.ToString();
        Icon = icon;
        Classes.Add(place.State.ToString().ToLowerInvariant());
        SetValue(AutomationProperties.NameProperty, $"{place.Name}. {StatusText}. {place.Reason}");
        ToolTip.SetTip(this, place.Reason);
    }

    /// <summary>Home's wording: one line under the name, the status when there is one, otherwise the reading summary. The accessible name reads both.</summary>
    public void ShowSummary(string? caption, string? status, Geometry? statusIcon, string reason)
    {
        Caption = status is null ? caption : null;
        StatusText = status;
        StatusIcon = statusIcon;
        SetValue(AutomationProperties.NameProperty, string.Join(". ", new[] { PlaceName, caption, status, reason.TrimEnd('.') }.Where(part => !string.IsNullOrWhiteSpace(part))) + ".");
    }
}

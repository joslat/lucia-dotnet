using Avalonia.Automation;
using Avalonia.Controls;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Lucia.Companion.App.Configuration;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>One catalog place and its explicit local import choice. No network, persistence or readings.</summary>
public sealed partial class HomeImportPlaceRow : UserControl
{
    private readonly string placeId;
    public bool IsSelected => this.FindControl<CheckBox>("SelectImportPlace")!.IsChecked == true;
    public HomeImportChoice Choice => new(placeId, this.FindControl<TextBox>("ImportPlaceName")!.Text ?? string.Empty,
        this.FindControl<ComboBox>("ImportPlaceIconChoice")!.SelectedItem is PlaceIcon icon ? icon : PlaceIcon.Home);
    public event Action? Changed;

    public HomeImportPlaceRow() : this(new HomePlaceCatalog("preview", "Preview place", []), null) { }

    public HomeImportPlaceRow(HomePlaceCatalog place, Place? existing)
    {
        placeId = place.PlaceId;
        AvaloniaXamlLoader.Load(this);
        this.FindControl<TextBlock>("ImportSourcePlaceName")!.Text = place.Name;
        this.FindControl<TextBlock>("ImportSourceCount")!.Text = $"{place.Sources.Count} {(place.Sources.Count == 1 ? "source" : "sources")} in the authorized catalog";
        var name = existing?.Name ?? SuggestName(place.Name);
        this.FindControl<TextBox>("ImportPlaceName")!.Text = name;
        var icons = this.FindControl<ComboBox>("ImportPlaceIconChoice")!;
        icons.ItemsSource = Enum.GetValues<PlaceIcon>();
        icons.SelectedItem = existing?.Icon ?? PlaceIcon.Home;
        this.FindControl<Path>("ImportPlaceIcon")!.Data = PlaceIcons.For(existing?.Icon ?? PlaceIcon.Home);
        this.FindControl<StackPanel>("NewPlaceFields")!.IsVisible = existing is null;
        this.FindControl<TextBlock>("ShortNameNotice")!.IsVisible = existing is null && name != place.Name;
        var kept = this.FindControl<TextBlock>("ExistingPlaceNotice")!;
        kept.IsVisible = existing is not null;
        kept.Text = existing is null ? string.Empty : $"Keep {existing.Name} and its existing icon, order and connections.";
        var check = this.FindControl<CheckBox>("SelectImportPlace")!;
        check.Tag = place.PlaceId;
        AutomationProperties.SetName(check, "Import " + place.Name);
        AutomationProperties.SetName(this.FindControl<TextBox>("ImportPlaceName")!, "Name for " + place.Name + " on this device");
        AutomationProperties.SetName(icons, "Icon for " + place.Name);
        foreach (var source in place.Sources)
        {
            this.FindControl<StackPanel>("ImportSourceNames")!.Children.Add(new TextBlock { Text = $"{source.Name} · {source.Field}", TextWrapping = TextWrapping.Wrap });
        }
        check.IsCheckedChanged += (_, _) => { this.FindControl<StackPanel>("SelectedPlaceFields")!.IsVisible = IsSelected; Changed?.Invoke(); };
        this.FindControl<TextBox>("ImportPlaceName")!.PropertyChanged += (_, change) => { if (change.Property == TextBox.TextProperty) { Changed?.Invoke(); } };
        icons.SelectionChanged += (_, _) =>
        {
            this.FindControl<Path>("ImportPlaceIcon")!.Data = PlaceIcons.For(Choice.Icon);
            Changed?.Invoke();
        };
    }

    private static string SuggestName(string value)
    {
        var length = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (length + rune.Utf16SequenceLength > 80) { break; }
            length += rune.Utf16SequenceLength;
        }
        return value[..length];
    }
}

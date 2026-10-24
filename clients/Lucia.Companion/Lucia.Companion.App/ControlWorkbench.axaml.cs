using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Lucia.Companion.App.Controls;
using Lucia.Companion.Core;

namespace Lucia.Companion.App;

/// <summary>Development host for atoms. Screen navigation and service orchestration do not belong here.</summary>
public sealed partial class ControlWorkbench : UserControl
{
    private readonly List<(Place Place, PlaceOrb Orb)> places = [];

    public ControlWorkbench()
    {
        AvaloniaXamlLoader.Load(this);
        BindThemeButton("SystemTheme", ThemeVariant.Default);
        BindThemeButton("LightTheme", ThemeVariant.Light);
        BindThemeButton("DarkTheme", ThemeVariant.Dark);

        Geometry[] icons = [PlaceIcons.Sofa, PlaceIcons.Garden, PlaceIcons.Kitchen, PlaceIcons.Door, PlaceIcons.Bed, PlaceIcons.Home];
        var panel = this.FindControl<WrapPanel>("PlacesPanel")!;
        for (var index = 0; index < WorkbenchFixtures.Places.Count; index++)
        {
            var place = WorkbenchFixtures.Places[index];
            var orb = new PlaceOrb(place, icons[index]);
            orb.Click += (_, _) => SelectPlace(place);
            places.Add((place, orb));
            panel.Children.Add(orb);
        }

        SelectPlace(places[0].Place);
    }

    private void BindThemeButton(string name, ThemeVariant theme) =>
        this.FindControl<Button>(name)!.Click += (_, _) => Application.Current!.RequestedThemeVariant = theme;

    private void SelectPlace(Place place)
    {
        foreach (var item in places)
        {
            item.Orb.IsSelected = item.Place.Id == place.Id;
        }

        this.FindControl<TextBlock>("SelectedName")!.Text = place.Name;
        this.FindControl<TextBlock>("SelectedReason")!.Text = place.Reason;
        var observations = this.FindControl<StackPanel>("ObservationsPanel")!;
        observations.Children.Clear();
        foreach (var observation in place.Observations)
        {
            observations.Children.Add(new ObservationRow(observation));
        }
    }
}

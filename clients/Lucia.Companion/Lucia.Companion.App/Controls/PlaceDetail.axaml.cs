using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

public sealed partial class PlaceDetail : UserControl
{
    public event Action? ActionsRequested;
    public PlaceDetail()
    {
        AvaloniaXamlLoader.Load(this);
        this.FindControl<Button>("ActionsButton")!.Click += (_, _) => ActionsRequested?.Invoke();
    }

    public void SetPlace(Place place, HomeTopology home)
    {
        this.FindControl<Avalonia.Controls.Shapes.Path>("PlaceIcon")!.Data = PlaceIcons.For(place.Icon);
        this.FindControl<TextBlock>("PlaceName")!.Text = place.Name;
        this.FindControl<TextBlock>("Reason")!.Text = $"{place.State}. {place.Reason}";
        var neighborIds = home.Connections.Where(edge => edge.FromPlaceId == place.Id || edge.ToPlaceId == place.Id)
            .Select(edge => edge.FromPlaceId == place.Id ? edge.ToPlaceId : edge.FromPlaceId).ToHashSet(StringComparer.Ordinal);
        var neighbors = home.Places.Where(candidate => neighborIds.Contains(candidate.Id)).Select(candidate => candidate.Name).ToArray();
        this.FindControl<TextBlock>("Connections")!.Text = neighbors.Length == 0
            ? "No physical connections configured. This is valid."
            : $"Connected to {string.Join(", ", neighbors)}.";
        var observations = this.FindControl<StackPanel>("Observations")!;
        observations.Children.Clear();
        foreach (var observation in place.Observations) { observations.Children.Add(new ObservationRow(observation)); }
        var empty = this.FindControl<TextBlock>("EmptyText")!;
        empty.IsVisible = place.Observations.Count == 0;
        empty.Text = ObservationBinding.MissingReadingsReason(home.Bindings.Any(binding => binding.PlaceId == place.Id));
    }

    public void FocusActions() => this.FindControl<Button>("ActionsButton")!.Focus();
    public Control GetPlaceAnchor() => this.FindControl<Border>("PlaceAnchor")!;
}

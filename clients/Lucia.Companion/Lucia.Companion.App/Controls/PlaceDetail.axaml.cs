using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

public sealed partial class PlaceDetail : UserControl
{
    public event Action? ActionsRequested;
    public event Action? PageRequested;
    public PlaceDetail()
    {
        AvaloniaXamlLoader.Load(this);
        this.FindControl<Button>("ActionsButton")!.Click += (_, _) => ActionsRequested?.Invoke();
        ContextHelp.Set(this.FindControl<Button>("ActionsButton")!, "Open this place's named shortcuts for readings, Story and pinning.");
        ContextHelp.Set(this.FindControl<Button>("OpenPlacePage")!, "Open this place's full page, including reading quality and explanations.");
        this.FindControl<Button>("OpenPlacePage")!.Click += (_, _) => PageRequested?.Invoke();
    }

    public void SetPlace(Place place, HomeTopology home)
    {
        this.FindControl<Avalonia.Controls.Shapes.Path>("PlaceIcon")!.Data = PlaceIcons.For(place.Icon);
        this.FindControl<TextBlock>("PlaceName")!.Text = place.Name;
        var status = this.FindControl<TextBlock>("StatusLine")!;
        status.Text = PlaceStateBrushes.StatusLabel(place);
        status[!TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(PlaceStateBrushes.Key(place.State));
        this.FindControl<TextBlock>("Reason")!.Text = place.Reason;
        var neighbors = home.NeighbourNames(place.Id);
        this.FindControl<TextBlock>("Connections")!.Text = neighbors.Count == 0
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

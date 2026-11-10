using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Lucia.Companion.Core;
using Path = Avalonia.Controls.Shapes.Path;

namespace Lucia.Companion.App.Controls;

/// <summary>
/// A place's full page. Readings and explanations come from the place itself; the chart and timeline are drawn only in the
/// demo, from labelled sample data, and connected mode says history isn't connected instead.
/// </summary>
public sealed partial class PlacePage : UserControl
{
    private string? placeId;
    public event Action? BackRequested;
    public event Action? PinToggled;
    public event Action? ActionsRequested;
    public event Action<string>? StoryRequested;

    public PlacePage()
    {
        AvaloniaXamlLoader.Load(this);
        this.FindControl<Button>("BackToHome")!.Click += (_, _) => BackRequested?.Invoke();
        this.FindControl<ToggleButton>("PinToggle")!.Click += (_, _) => PinToggled?.Invoke();
        this.FindControl<Button>("PageActionsButton")!.Click += (_, _) => ActionsRequested?.Invoke();
        this.FindControl<Button>("OpenStory")!.Click += (_, _) => { if (placeId is not null) { StoryRequested?.Invoke(placeId); } };
        var why = this.FindControl<ToggleButton>("WhyShown")!;
        why.IsCheckedChanged += (_, _) => this.FindControl<TextBlock>("WhyText")!.IsVisible = why.IsChecked == true;
    }

    public void SetPlace(Place place, HomeTopology home, bool isDemo, DateTimeOffset? reportTime)
    {
        ArgumentNullException.ThrowIfNull(place);
        ArgumentNullException.ThrowIfNull(home);
        if (placeId != place.Id) { this.FindControl<ToggleButton>("WhyShown")!.IsChecked = false; }
        placeId = place.Id;
        var brush = PlaceStateBrushes.Key(place.State);
        this.FindControl<TextBlock>("Breadcrumb")!.Text = place.Zone is { } zone ? $"{home.Name} · {PlaceZones.Label(zone)}" : home.Name;
        this.FindControl<TextBlock>("PageTitle")!.Text = place.Name;

        var ring = this.FindControl<Ellipse>("HeroRing")!;
        ring[!Shape.StrokeProperty] = new DynamicResourceExtension(place.State is PlaceState.Quiet ? "GraphLineBrush" : brush);
        ring.StrokeDashArray = place.State == PlaceState.Unavailable ? [2, 4] : null;
        var icon = this.FindControl<Path>("HeroIcon")!;
        icon.Data = PlaceIcons.For(place.Icon);
        icon[!Shape.StrokeProperty] = new DynamicResourceExtension(place.State is PlaceState.Quiet ? "TextBrush" : brush);

        var chip = this.FindControl<Border>("StatusChip")!;
        chip[!Border.BorderBrushProperty] = new DynamicResourceExtension(brush);
        var chipText = this.FindControl<TextBlock>("StatusChipText")!;
        chipText.Text = PlaceStateBrushes.StatusLabel(place);
        chipText[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(brush);
        var chipIcon = this.FindControl<Path>("StatusChipIcon")!;
        chipIcon.Data = place.State switch
        {
            PlaceState.Problem or PlaceState.Waiting or PlaceState.Unavailable => AttentionIcons.For(place.State),
            PlaceState.Working => NavigationIcons.Lucia,
            _ => null,
        };
        chipIcon[!Shape.StrokeProperty] = new DynamicResourceExtension(brush);
        this.FindControl<Viewbox>("StatusChipIconBox")!.IsVisible = chipIcon.Data is not null;

        this.FindControl<TextBlock>("ExplanationTitle")!.Text = place.Reason;
        this.FindControl<TextBlock>("ExplanationBody")!.Text = PlaceExplanation.Body(place);
        this.FindControl<TextBlock>("WhyText")!.Text = PlaceExplanation.WhyShown(place.State);
        var neighborIds = home.Connections.Where(edge => edge.FromPlaceId == place.Id || edge.ToPlaceId == place.Id)
            .Select(edge => edge.FromPlaceId == place.Id ? edge.ToPlaceId : edge.FromPlaceId).ToHashSet(StringComparer.Ordinal);
        var neighbors = home.Places.Where(candidate => neighborIds.Contains(candidate.Id)).Select(candidate => candidate.Name).ToArray();
        this.FindControl<TextBlock>("PageConnections")!.Text = neighbors.Length == 0
            ? "No physical connections configured. This is valid." : $"Connected to {string.Join(", ", neighbors)}.";

        this.FindControl<TextBlock>("ReadingsTitle")!.Text = $"{place.Name} now";
        var observations = this.FindControl<StackPanel>("PageObservations")!;
        observations.Children.Clear();
        foreach (var observation in place.Observations) { observations.Children.Add(new ObservationRow(observation)); }
        var empty = this.FindControl<TextBlock>("PageEmptyText")!;
        empty.IsVisible = place.Observations.Count == 0;
        empty.Text = ObservationBinding.MissingReadingsReason(home.Bindings.Any(binding => binding.PlaceId == place.Id));

        this.FindControl<TextBlock>("HistoryNote")!.IsVisible = !isDemo;
        ShowSampleChart(place, isDemo, reportTime);
        ShowSampleTimeline(place, home, isDemo);
    }

    public void SetPinned(bool pinned)
    {
        var pin = this.FindControl<ToggleButton>("PinToggle")!;
        pin.IsChecked = pinned;
        AutomationProperties.SetName(pin, pinned ? "Unpin this place" : "Pin this place");
    }

    public void FocusBack() => this.FindControl<Button>("BackToHome")!.Focus();
    public void FocusActions() => this.FindControl<Button>("PageActionsButton")!.Focus();
    public Control GetPlaceAnchor() => this.FindControl<Grid>("PlaceAnchor")!;

    private void ShowSampleChart(Place place, bool isDemo, DateTimeOffset? reportTime)
    {
        var card = this.FindControl<Border>("ChartCard")!;
        var chartable = isDemo && reportTime is { } now ? SamplePlaceHistory.Chartable(place) : null;
        card.IsVisible = chartable is not null;
        if (chartable is not { } item || reportTime is not { } time) { return; }
        var lastAt = item.Observation.ObservedAt!.Value;
        var dayStart = new DateTimeOffset(time.UtcDateTime.Date, TimeSpan.Zero);
        var title = item.Observation.Label;
        this.FindControl<TextBlock>("ChartTitle")!.Text = $"{title} today";
        this.FindControl<TextBlock>("ChartCaption")!.Text = $"Sample history · last reading {item.Observation.Value} at {lastAt.UtcDateTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)} UTC";
        this.FindControl<ReadingChart>("TodayChart")!.SetSeries(SamplePlaceHistory.Series(title, item.Value, dayStart, lastAt),
            dayStart, dayStart.AddDays(1), time, item.Unit, title, item.Observation.Quality == ObservationQuality.Current);
    }

    private void ShowSampleTimeline(Place place, HomeTopology home, bool isDemo)
    {
        var card = this.FindControl<Border>("TimelineCard")!;
        card.IsVisible = isDemo;
        if (!isDemo) { return; }
        var day = SampleStory.Day(home, SampleStory.Today);
        this.FindControl<TextBlock>("TimelineTitle")!.Text = $"{place.Name} in the sample story";
        this.FindControl<TextBlock>("TimelineCaption")!.Text = $"Sample story day · {day.Date.ToString("dddd d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture)} · {day.TimeZone.Id}";
        var entries = this.FindControl<StackPanel>("TimelineEntries")!;
        entries.Children.Clear();
        var intervals = day.Intervals.Where(interval => interval.PlaceId == place.Id).OrderBy(interval => interval.Start).ToArray();
        foreach (var interval in intervals)
        {
            entries.Children.Add(new TextBlock
            {
                Text = $"{day.TimeLabel(interval.Start)[..5]}–{day.TimeLabel(interval.End)[..5]} · {interval.Label}",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap, [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize14"),
            });
        }
        if (intervals.Length == 0)
        {
            entries.Children.Add(new TextBlock { Text = "Nothing reported for this place in the sample story.", Classes = { "muted" } });
        }
    }
}

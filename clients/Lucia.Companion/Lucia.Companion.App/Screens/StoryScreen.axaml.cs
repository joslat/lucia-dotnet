using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Screens;

public sealed partial class StoryScreen : UserControl
{
    private bool refreshing;
    private string? renderedFilter;
    private bool hasTimelines;
    public StoryViewModel State { get; }
    public bool IsExpanded => Bounds.Width >= 900;

    public StoryScreen() : this(new StoryViewModel(SampleStory.Create(SampleHome.Create()))) { }
    public StoryScreen(StoryViewModel state)
    {
        State = state;
        AvaloniaXamlLoader.Load(this);
        this.FindControl<TextBlock>("DayLabel")!.Text = $"{state.Day.Date:dddd d MMMM yyyy} · {state.Day.TimeZone.Id} · {(state.Day.End - state.Day.Start).TotalHours:0}-hour day";
        this.FindControl<StoryOrbit>("Orbit")!.TimeInvoked += state.SelectFraction;
        this.FindControl<Slider>("TimeScrubber")!.PropertyChanged += (_, change) =>
        {
            if (!refreshing && change.Property == Slider.ValueProperty) { state.SelectFraction((double)change.NewValue!); }
        };
        this.FindControl<CheckBox>("AnimateSelection")!.IsCheckedChanged += (_, _) => Refresh();
        AttachedToVisualTree += (_, _) => { state.PropertyChanged += StateChanged; Refresh(); };
        DetachedFromVisualTree += (_, _) => state.PropertyChanged -= StateChanged;
        SizeChanged += (_, _) => Refresh();
        this.FindControl<TextBlock>("CenterTime")!.PropertyChanged += (_, change) =>
        {
            if (change.Property == TextBlock.FontSizeProperty) { Refresh(); }
        };
        Refresh();
    }

    private void StateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => Refresh();

    private void Refresh()
    {
        if (refreshing) { return; }
        refreshing = true;
        try
        {
            var grid = this.FindControl<Grid>("StoryGrid")!;
            grid.ColumnDefinitions[0].Width = IsExpanded ? new GridLength(420) : GridLength.Star;
            grid.ColumnDefinitions[1].Width = IsExpanded ? GridLength.Star : new GridLength(0);
            grid.ColumnSpacing = IsExpanded ? 24 : 0;
            var chapters = this.FindControl<StackPanel>("ChapterPanel")!;
            Grid.SetColumn(chapters, IsExpanded ? 1 : 0);
            Grid.SetRow(chapters, IsExpanded ? 0 : 1);
            var side = IsExpanded ? 400 : Math.Clamp(Bounds.Width - 32, 240, 340);
            var frame = this.FindControl<Grid>("DialFrame")!;
            frame.Width = frame.Height = side;
            var time = State.Day.TimeLabel(State.SelectedInstant);
            var largeClock = this.FindControl<TextBlock>("CenterTime")!.FontSize > 36;
            this.FindControl<TextBlock>("CenterTime")!.IsVisible = !largeClock;
            this.FindControl<TextBlock>("CenterOffset")!.IsVisible = !largeClock;
            this.FindControl<TextBlock>("LargeSelectedTime")!.IsVisible = largeClock;
            this.FindControl<TextBlock>("LargeSelectedTime")!.Text = $"Selected time · {time}";
            this.FindControl<TextBlock>("CenterTime")!.Text = time[..5];
            this.FindControl<TextBlock>("CenterOffset")!.Text = $"UTC{time[6..]}";
            this.FindControl<TextBlock>("CenterCaption")!.Text = largeClock ? "Time\nbelow" : State.SelectedChapterId is null ? "Selected time" : "Chapter start";
            foreach (var (name, fraction) in new[] { ("TopTime", 0d), ("QuarterTime", .25), ("HalfTime", .5), ("ThreeQuarterTime", .75) })
            {
                this.FindControl<TextBlock>(name)!.Text = State.Day.TimeLabel(State.Day.InstantAt(fraction))[..5];
            }
            var orbit = this.FindControl<StoryOrbit>("Orbit")!;
            orbit.SetState(State, this.FindControl<CheckBox>("AnimateSelection")!.IsChecked == true);
            var ringPlaces = State.VisiblePlaces.Take(6).ToArray();
            this.FindControl<TextBlock>("RingOrder")!.Text = ringPlaces.Length == 0 ? "No places configured." :
                $"Outer to inner: {string.Join(", ", ringPlaces.Select(place => place.Name))}. Showing {ringPlaces.Length} of {State.VisiblePlaces.Count} places.";
            var scrubber = this.FindControl<Slider>("TimeScrubber")!;
            scrubber.Value = State.CursorFraction;
            scrubber.SmallChange = 60 / (State.Day.End - State.Day.Start).TotalSeconds;
            scrubber.LargeChange = 3600 / (State.Day.End - State.Day.Start).TotalSeconds;
            scrubber.SetValue(AutomationProperties.HelpTextProperty, $"Selected {time}. Arrow keys move one elapsed minute. Page keys move one elapsed hour.");
            this.FindControl<PlaceFilterBar>("PlaceFilters")!.SetState(State);
            this.FindControl<ChapterList>("Chapters")!.SetState(State);
            if (!hasTimelines || renderedFilter != State.SelectedPlaceId)
            {
                var timelines = this.FindControl<StackPanel>("Timelines")!;
                timelines.Children.Clear();
                foreach (var place in State.VisiblePlaces) { timelines.Children.Add(new PlaceTimeline(State, place)); }
                renderedFilter = State.SelectedPlaceId;
                hasTimelines = true;
            }
            foreach (var timeline in this.FindControl<StackPanel>("Timelines")!.Children.OfType<PlaceTimeline>()) { timeline.RefreshSelection(); }
            this.FindControl<TextBlock>("SelectedReadout")!.Text = State.SelectedPlaceId is { } id
                ? $"{time}. " + string.Join(" · ", Enum.GetValues<StoryKind>().Select(kind =>
                {
                    var report = State.Day.At(id, kind, State.SelectedInstant).FirstOrDefault();
                    return $"{StoryDrawing.Label(kind)}: {(report is null ? "Unknown" : $"{report.Label} [{report.Quality}]")}";
                }))
                : $"Cursor: {time}. Select a place for the four track readings at this instant. Reports below select their own place and time.";
        }
        finally { refreshing = false; }
    }
}

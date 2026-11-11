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
    private bool attached;
    private string? renderedFilter;
    private bool hasTimelines;
    private readonly Dictionary<string, PlaceTimeline> timelineCache = new(StringComparer.Ordinal);
    private readonly StoryNavigator? navigator;
    private (DateOnly Date, string? Place)? renderedWeek;

    /// <summary>The day currently shown. With a navigator this changes as the person moves between days.</summary>
    public StoryViewModel State { get; private set; }
    /// <summary>Dial and chapters sit side by side from 720 units, which a portrait 834 tablet has beside the rail.</summary>
    public bool IsExpanded => Bounds.Width >= 720;

    public StoryScreen() : this(new StoryViewModel(SampleStory.Create(SampleHome.Create()))) { }

    /// <summary>A single day with no date navigation.</summary>
    public StoryScreen(StoryViewModel state) : this(state, null) { }

    /// <summary>Story with Day/Week and previous/next dates.</summary>
    public StoryScreen(StoryNavigator navigator) : this(navigator.Day, navigator) { }

    private StoryScreen(StoryViewModel state, StoryNavigator? navigator)
    {
        State = state;
        this.navigator = navigator;
        AvaloniaXamlLoader.Load(this);
        this.FindControl<StoryOrbit>("Orbit")!.TimeInvoked += fraction => State.SelectFraction(fraction);
        this.FindControl<Slider>("TimeScrubber")!.PropertyChanged += (_, change) =>
        {
            if (!refreshing && change.Property == Slider.ValueProperty) { State.SelectFraction((double)change.NewValue!); }
        };
        this.FindControl<CheckBox>("AnimateSelection")!.IsCheckedChanged += (_, _) => Refresh();
        this.FindControl<StackPanel>("StoryNavigation")!.IsVisible = navigator is not null;
        if (navigator is not null)
        {
            this.FindControl<Button>("PreviousDate")!.Click += (_, _) => navigator.Previous();
            this.FindControl<Button>("NextDate")!.Click += (_, _) => navigator.Next();
            this.FindControl<RadioButton>("DayMode")!.IsCheckedChanged += (sender, _) =>
            {
                if (!refreshing && sender is RadioButton { IsChecked: true }) { navigator.SetMode(StoryMode.Day); }
            };
            this.FindControl<RadioButton>("WeekMode")!.IsCheckedChanged += (sender, _) =>
            {
                if (!refreshing && sender is RadioButton { IsChecked: true }) { navigator.SetMode(StoryMode.Week); }
            };
            this.FindControl<StoryWeek>("Week")!.DayRequested += navigator.ShowDay;
            navigator.PropertyChanged += (_, _) => NavigatorChanged();
        }
        AttachedToVisualTree += (_, _) => { attached = true; State.PropertyChanged += StateChanged; MotionSettings.Current.Changed += Refresh; Refresh(); };
        DetachedFromVisualTree += (_, _) => { attached = false; State.PropertyChanged -= StateChanged; MotionSettings.Current.Changed -= Refresh; };
        SizeChanged += (_, _) => Refresh();
        this.FindControl<TextBlock>("CenterTime")!.PropertyChanged += (_, change) =>
        {
            if (change.Property == TextBlock.FontSizeProperty) { Refresh(); }
        };
        Refresh();
    }

    /// <summary>The wide-layout summary for Week: totals across the days shown, with no attention count, which belongs to today.</summary>
    private void RefreshWeekStats(IReadOnlyList<StoryWeekDay> week)
    {
        var recorded = week.Sum(day => day.ReportCount);
        var requests = week.Sum(day => day.Intervals.Count(interval => interval.Kind == StoryKind.Lucia && interval.Quality == StoryQuality.Recorded));
        var stats = this.FindControl<TextBlock>("StatsLine")!;
        stats.Text = $"{(recorded == 1 ? "1 report" : $"{recorded} reports")} · {(requests == 1 ? "1 Lucia request" : $"{requests} Lucia requests")} this week";
        stats.IsVisible = IsExpanded;
    }

    /// <summary>The wide-layout summary: reports and Lucia requests in this day, and, for today only, how many places need you now.</summary>
    private void RefreshStats(int attention)
    {
        var recorded = State.Day.Intervals.Count(interval => interval.Quality == StoryQuality.Recorded);
        var requests = State.Day.Intervals.Count(interval => interval.Kind == StoryKind.Lucia && interval.Quality == StoryQuality.Recorded);
        var parts = new List<string> { recorded == 1 ? "1 report" : $"{recorded} reports", requests == 1 ? "1 Lucia request" : $"{requests} Lucia requests" };
        if (State.Day.Now is not null && attention > 0) { parts.Add(attention == 1 ? "1 reason for attention" : $"{attention} reasons for attention"); }
        var stats = this.FindControl<TextBlock>("StatsLine")!;
        stats.Text = string.Join(" · ", parts);
        stats.IsVisible = IsExpanded;
    }

    /// <summary>Lucia's requests across the visible places on one lane, so they can be read apart from the place tracks.</summary>
    private void RefreshLuciaLane()
    {
        var visible = State.VisiblePlaces.Select(place => place.Id).ToHashSet(StringComparer.Ordinal);
        var requests = State.Day.Intervals.Where(interval => interval.Kind == StoryKind.Lucia && visible.Contains(interval.PlaceId)).ToArray();
        this.FindControl<WeekDayLane>("LuciaLane")!.SetDay(State.Day, requests);
        this.FindControl<TextBlock>("LuciaLaneCaption")!.Text = requests.Length == 0
            ? State.SelectedPlaceId is null ? "No Lucia requests in this day." : "No Lucia requests for this place in this day."
            : $"{requests.Length} Lucia request{(requests.Length == 1 ? string.Empty : "s")} in this day, {(State.SelectedPlaceId is null ? "from any place" : "for this place")}.";
    }

    /// <summary>Moves to today's day view when this Story can navigate dates.</summary>
    public void ShowToday() => navigator?.ShowDay(navigator.Today);

    private void StateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => Refresh();

    private void NavigatorChanged()
    {
        if (navigator is null) { return; }
        if (!ReferenceEquals(State, navigator.Day))
        {
            if (attached) { State.PropertyChanged -= StateChanged; }
            State = navigator.Day;
            if (attached) { State.PropertyChanged += StateChanged; }
            hasTimelines = false;
            timelineCache.Clear();
        }
        Refresh();
    }

    private void Refresh()
    {
        if (refreshing) { return; }
        refreshing = true;
        try
        {
            this.FindControl<TextBlock>("DayLabel")!.Text = $"{State.Day.Date:dddd d MMMM yyyy} · {State.Day.TimeZone.Id} · {(State.Day.End - State.Day.Start).TotalHours:0}-hour day";
            var week = navigator?.Mode == StoryMode.Week;
            this.FindControl<TextBlock>("StoryTitle")!.Text = week ? "The story of your week" : "The story of your day";
            if (navigator is not null)
            {
                this.FindControl<TextBlock>("DateLabel")!.Text = navigator.DateLabel;
                this.FindControl<Button>("PreviousDate")!.IsEnabled = navigator.CanGoPrevious;
                this.FindControl<Button>("NextDate")!.IsEnabled = navigator.CanGoNext;
                this.FindControl<RadioButton>("DayMode")!.IsChecked = !week;
                this.FindControl<RadioButton>("WeekMode")!.IsChecked = week;
                if (!week) { renderedWeek = null; }
                if (week)
                {
                    if (renderedWeek != (navigator.Date, State.SelectedPlaceId))
                    {
                        var days = navigator.Week;
                        this.FindControl<StoryWeek>("Week")!.SetWeek(days, navigator.Date);
                        RefreshWeekStats(days);
                        renderedWeek = (navigator.Date, State.SelectedPlaceId);
                    }
                    this.FindControl<TextBlock>("StatsLine")!.IsVisible = IsExpanded;
                    this.FindControl<TextBlock>("DayLabel")!.Text =
                        $"{navigator.Dates[0]:dddd d} to {navigator.Dates[^1]:dddd d MMMM yyyy} · {State.Day.TimeZone.Id}";
                }
            }
            this.FindControl<StoryWeek>("Week")!.IsVisible = week;
            this.FindControl<Grid>("StoryGrid")!.IsVisible = !week;
            this.FindControl<StackPanel>("DayDetails")!.IsVisible = !week;
            this.FindControl<PlaceFilterBar>("PlaceFilters")!.SetState(State);
            if (week) { return; }

            var grid = this.FindControl<Grid>("StoryGrid")!;
            var expandedDial = Math.Clamp((Bounds.Width - 32) * 0.5, 300, 400);
            grid.ColumnDefinitions[0].Width = IsExpanded ? new GridLength(expandedDial + 20) : GridLength.Star;
            grid.ColumnDefinitions[1].Width = IsExpanded ? GridLength.Star : new GridLength(0);
            grid.ColumnSpacing = IsExpanded ? 24 : 0;
            var chapters = this.FindControl<StackPanel>("ChapterPanel")!;
            Grid.SetColumn(chapters, IsExpanded ? 1 : 0);
            Grid.SetRow(chapters, IsExpanded ? 0 : 1);
            var side = IsExpanded ? expandedDial : Math.Clamp(Bounds.Width - 32, 240, 340);
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
            var atNow = State.Day.Now is { } now && State.SelectedInstant == now;
            var attention = navigator?.AttentionCount ?? 0;
            this.FindControl<TextBlock>("CenterCaption")!.Text = largeClock ? "Time\nbelow"
                : atNow ? attention > 0 ? $"now · {attention} need you" : "now"
                : State.SelectedChapterId is null ? "Selected time" : "Chapter start";
            RefreshStats(attention);
            RefreshLuciaLane();
            foreach (var (name, fraction) in new[] { ("TopTime", 0d), ("QuarterTime", .25), ("HalfTime", .5), ("ThreeQuarterTime", .75) })
            {
                this.FindControl<TextBlock>(name)!.Text = State.Day.TimeLabel(State.Day.InstantAt(fraction))[..5];
            }
            var orbit = this.FindControl<StoryOrbit>("Orbit")!;
            var animateSelection = this.FindControl<CheckBox>("AnimateSelection")!.IsChecked == true && MotionSettings.Current.IsEnabled;
            orbit.SetState(State, animateSelection);
            var ringPlaces = State.VisiblePlaces.Take(6).ToArray();
            this.FindControl<TextBlock>("RingOrder")!.Text = ringPlaces.Length == 0 ? "No places configured." :
                $"Outer to inner: {string.Join(", ", ringPlaces.Select(place => place.Name))}. Showing {ringPlaces.Length} of {State.VisiblePlaces.Count} places.";
            var scrubber = this.FindControl<Slider>("TimeScrubber")!;
            scrubber.Value = State.CursorFraction;
            scrubber.SmallChange = 60 / (State.Day.End - State.Day.Start).TotalSeconds;
            scrubber.LargeChange = 3600 / (State.Day.End - State.Day.Start).TotalSeconds;
            scrubber.SetValue(AutomationProperties.HelpTextProperty, $"Selected {time}. Arrow keys move one elapsed minute. Page keys move one elapsed hour.");
            this.FindControl<ChapterList>("Chapters")!.SetState(State);
            if (!hasTimelines || renderedFilter != State.SelectedPlaceId)
            {
                var timelines = this.FindControl<StackPanel>("Timelines")!;
                var next = State.VisiblePlaces.Select(place =>
                {
                    if (!timelineCache.TryGetValue(place.Id, out var timeline)) { timelineCache[place.Id] = timeline = new PlaceTimeline(State, place); }
                    return timeline;
                }).ToArray();
                foreach (var obsolete in timelines.Children.Where(item => !next.Contains(item)).ToArray()) { timelines.Children.Remove(obsolete); }
                for (var index = 0; index < next.Length; index++)
                {
                    var current = timelines.Children.IndexOf(next[index]);
                    if (current < 0) { timelines.Children.Insert(index, next[index]); }
                    else if (current != index) { timelines.Children.Move(current, index); }
                }
                renderedFilter = State.SelectedPlaceId;
                hasTimelines = true;
            }
            foreach (var timeline in this.FindControl<StackPanel>("Timelines")!.Children.OfType<PlaceTimeline>()) { timeline.RefreshSelection(animateSelection); }
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

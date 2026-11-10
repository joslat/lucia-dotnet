using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>Home's "Today so far": one line of today's reports up to now, labelled as a sample, with a link into Story.</summary>
public sealed partial class TodaySoFar : UserControl
{
    public event Action? StoryRequested;
    public StoryDay? Day { get; private set; }
    public WeekDayLane Lane => this.FindControl<WeekDayLane>("TodayLane")!;

    public TodaySoFar()
    {
        AvaloniaXamlLoader.Load(this);
        this.FindControl<Button>("OpenTodayStory")!.Click += (_, _) => StoryRequested?.Invoke();
    }

    public void SetDay(StoryDay day)
    {
        ArgumentNullException.ThrowIfNull(day);
        Day = day;
        Lane.SetDay(day, day.Intervals);
        var reports = day.Intervals.Count(interval => interval.Quality == StoryQuality.Recorded);
        var requests = day.Intervals.Count(interval => interval.Kind == StoryKind.Lucia && interval.Quality == StoryQuality.Recorded);
        var caption = $"Sample day · {reports} report{(reports == 1 ? string.Empty : "s")} so far · {requests} Lucia request{(requests == 1 ? string.Empty : "s")}";
        this.FindControl<TextBlock>("TodayCaption")!.Text = caption;
        AutomationProperties.SetName(Lane, $"Today so far. {caption}. The rest of the day is not known yet.");
    }
}

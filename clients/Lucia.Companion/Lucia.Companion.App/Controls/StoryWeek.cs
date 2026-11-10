using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>
/// The week at a glance: one row per day with its reports on a 24-hour lane and a count. A row opens that day. It shows
/// what was reported each day, not an inferred hour-by-hour pattern.
/// </summary>
public sealed class StoryWeek : UserControl
{
    private readonly StackPanel rows = new() { Spacing = 6 };
    private readonly List<Button> buttons = [];
    public event Action<DateOnly>? DayRequested;
    public IReadOnlyList<Button> Rows => buttons;

    public StoryWeek()
    {
        var content = new StackPanel { Spacing = 10 };
        var scale = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(12, 0) };
        var ticks = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), [Grid.ColumnProperty] = 1 };
        foreach (var (hour, column) in new[] { ("00", 0), ("06", 1), ("12", 2), ("18", 3) })
        {
            ticks.Children.Add(new TextBlock { Text = hour, Classes = { "muted" }, [Grid.ColumnProperty] = column, [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize12") });
        }
        scale.Children.Add(new Border { Width = 116 });
        scale.Children.Add(ticks);
        scale.Children.Add(new Border { Width = 84, [Grid.ColumnProperty] = 2 });
        content.Children.Add(scale);
        content.Children.Add(rows);
        content.Children.Add(new TextBlock
        {
            Text = "Each row is one sample day's reports. Empty track means no report, not that nothing happened.",
            Classes = { "muted" }, [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize13"),
        });
        Content = content;
    }

    public void SetWeek(IReadOnlyList<StoryWeekDay> week, DateOnly selected)
    {
        ArgumentNullException.ThrowIfNull(week);
        rows.Children.Clear();
        buttons.Clear();
        foreach (var day in week)
        {
            var lane = new WeekDayLane { VerticalAlignment = VerticalAlignment.Center, [Grid.ColumnProperty] = 1 };
            lane.SetDay(day.Day, day.Intervals);
            var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("116,*,84"), ColumnSpacing = 0 };
            layout.Children.Add(new TextBlock
            {
                Text = day.Label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap,
                FontWeight = day.Date == selected ? FontWeight.SemiBold : FontWeight.Normal,
            });
            layout.Children.Add(lane);
            layout.Children.Add(new TextBlock
            {
                Text = day.ReportCount == 1 ? "1 report" : $"{day.ReportCount} reports", Classes = { "muted" }, TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center, [Grid.ColumnProperty] = 2, [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize13"),
            });
            var button = new Button
            {
                Content = layout, Tag = day.Date, MinHeight = 48, Padding = new Thickness(12, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Classes = { "theme" },
            };
            button.Classes.Set("selected", day.Date == selected);
            AutomationProperties.SetName(button, $"{day.Label}. {(day.ReportCount == 1 ? "1 report" : $"{day.ReportCount} reports")}. Open this day.");
            var date = day.Date;
            button.Click += (_, _) => DayRequested?.Invoke(date);
            buttons.Add(button);
            rows.Children.Add(button);
        }
    }
}

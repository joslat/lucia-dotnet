using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
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
    private readonly Grid scale = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(12, 0) };
    private readonly Grid ticks = new() { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), [Grid.ColumnProperty] = 1 };
    private readonly Border labelSpace = new() { Width = 116 };
    private readonly Border countSpace = new() { Width = 84, [Grid.ColumnProperty] = 2 };
    private readonly List<(Grid Layout, TextBlock Label, TextBlock Count, WeekDayLane Lane)> layouts = [];
    private (bool Stacked, double LabelWidth, double CountWidth, bool DenseTicks)? arrangement;
    private bool adaptQueued;
    public event Action<DateOnly>? DayRequested;
    public IReadOnlyList<Button> Rows => buttons;

    public StoryWeek()
    {
        var content = new StackPanel { Spacing = 10 };
        foreach (var (hour, column) in new[] { ("00", 0), ("06", 1), ("12", 2), ("18", 3) })
        {
            var label = new TextBlock { Text = hour, TextWrapping = TextWrapping.NoWrap, Classes = { "muted" }, [Grid.ColumnProperty] = column, [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize12") };
            WatchType(label);
            ticks.Children.Add(label);
        }
        scale.Children.Add(labelSpace);
        scale.Children.Add(ticks);
        scale.Children.Add(countSpace);
        content.Children.Add(scale);
        content.Children.Add(rows);
        content.Children.Add(new TextBlock
        {
            Text = "Each row is one sample day's reports. Empty track means no report, not that nothing happened.",
            Classes = { "muted" }, [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize13"),
        });
        Content = content;
        SizeChanged += (_, _) => Adapt();
        AttachedToVisualTree += (_, _) => QueueAdapt();
    }

    public void SetWeek(IReadOnlyList<StoryWeekDay> week, DateOnly selected)
    {
        ArgumentNullException.ThrowIfNull(week);
        rows.Children.Clear();
        buttons.Clear();
        layouts.Clear();
        arrangement = null;
        foreach (var day in week)
        {
            var lane = new WeekDayLane { VerticalAlignment = VerticalAlignment.Center, [Grid.ColumnProperty] = 1 };
            lane.SetDay(day.Day, day.Intervals);
            var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("116,*,84"), ColumnSpacing = 0 };
            var label = new TextBlock
            {
                Text = day.Label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap,
                FontWeight = day.Date == selected ? FontWeight.SemiBold : FontWeight.Normal,
            };
            layout.Children.Add(label);
            layout.Children.Add(lane);
            var count = new TextBlock
            {
                Text = day.ReportCount == 1 ? "1 report" : $"{day.ReportCount} reports", Classes = { "muted" }, TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.NoWrap, VerticalAlignment = VerticalAlignment.Center, [Grid.ColumnProperty] = 2, [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize13"),
            };
            layout.Children.Add(count);
            WatchType(label); WatchType(count);
            layouts.Add((layout, label, count, lane));
            var button = new Button
            {
                Content = layout, Tag = day.Date, MinHeight = 48, Padding = new Thickness(12, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Classes = { "theme" },
            };
            button.Classes.Set("selected", day.Date == selected);
            AutomationProperties.SetName(button, $"{day.Label}. {(day.Date == selected ? "Selected day. " : string.Empty)}{(day.ReportCount == 1 ? "1 report" : $"{day.ReportCount} reports")}. Open this day.");
            var date = day.Date;
            button.Click += (_, _) => DayRequested?.Invoke(date);
            buttons.Add(button);
            rows.Children.Add(button);
        }
        Adapt();
    }

    private void WatchType(TextBlock text) => text.PropertyChanged += (_, args) =>
    {
        if (args.Property == TextBlock.FontSizeProperty || args.Property == TextBlock.FontFamilyProperty) { QueueAdapt(); }
    };

    private void QueueAdapt()
    {
        if (adaptQueued) { return; }
        adaptQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            adaptQueued = false;
            if (TopLevel.GetTopLevel(this) is not null) { Adapt(); }
        }, DispatcherPriority.Loaded);
    }

    private void Adapt()
    {
        if (Bounds.Width <= 0 || layouts.Count == 0) { return; }
        static double NaturalWidth(TextBlock text)
        {
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return Math.Ceiling(text.DesiredSize.Width);
        }
        var labelWidth = Math.Max(116, layouts.Max(row => NaturalWidth(row.Label)) + 12);
        var countWidth = Math.Max(84, layouts.Max(row => NaturalWidth(row.Count)) + 12);
        var rulerWidth = ticks.Children.OfType<TextBlock>().Max(NaturalWidth);
        var minimumLane = Math.Max(120, 4 * (rulerWidth + 8));
        var available = Math.Max(0, Bounds.Width - 24);
        var stacked = available - labelWidth - countWidth < minimumLane;
        var laneWidth = stacked ? available : available - labelWidth - countWidth;
        var dense = laneWidth >= 4 * (rulerWidth + 8);
        var next = (stacked, labelWidth, countWidth, dense);
        if (arrangement == next) { return; }
        arrangement = next;
        labelSpace.Width = stacked ? 0 : labelWidth;
        countSpace.Width = stacked ? 0 : countWidth;
        foreach (var tick in ticks.Children.OfType<TextBlock>()) { tick.IsVisible = dense || tick.Text is "00" or "12"; }
        foreach (var row in layouts)
        {
            row.Layout.ColumnDefinitions = stacked
                ? new ColumnDefinitions("*,Auto")
                : new ColumnDefinitions { new(new GridLength(labelWidth)), new(GridLength.Star), new(new GridLength(countWidth)) };
            row.Layout.RowDefinitions = new RowDefinitions(stacked ? "Auto,Auto" : "Auto");
            Grid.SetColumn(row.Count, stacked ? 1 : 2);
            Grid.SetColumn(row.Lane, stacked ? 0 : 1);
            Grid.SetRow(row.Lane, stacked ? 1 : 0);
            Grid.SetColumnSpan(row.Lane, stacked ? 2 : 1);
            row.Lane.Margin = stacked ? new Thickness(0, 8, 0, 0) : default;
            row.Label.Margin = stacked ? new Thickness(0, 0, 12, 0) : default;
        }
    }
}

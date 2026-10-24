using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

public sealed class PlaceTimeline : UserControl
{
    public StoryViewModel State { get; }
    private readonly TimelineTrackLayer track;

    public PlaceTimeline(StoryViewModel state, PlaceSetting place)
    {
        State = state;
        track = new TimelineTrackLayer { State = state, PlaceId = place.Id };
        track.TimeInvoked += state.SelectFraction;
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var icon = new Avalonia.Controls.Shapes.Path { Data = PlaceIcons.For(place.Icon), Width = 20, Height = 20, Stretch = Stretch.Uniform, StrokeThickness = 1.7 };
        icon[!Avalonia.Controls.Shapes.Path.StrokeProperty] = new DynamicResourceExtension("MutedBrush");
        title.Children.Add(icon);
        title.Children.Add(new TextBlock { Text = place.Name, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(title);
        stack.Children.Add(track);
        stack.Children.Add(new TimelineRuler(state.Day));
        var reports = state.Day.Intervals.Where(interval => interval.PlaceId == place.Id).ToArray();
        var buttons = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var report in reports)
        {
            var reportContent = new StackPanel { Spacing = 2 };
            reportContent.Children.Add(new TextBlock { Text = StoryDrawing.RangeLabel(state.Day, report.Start, report.End),
                [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize13"), Classes = { "muted" }, MaxWidth = 320, TextWrapping = TextWrapping.Wrap });
            reportContent.Children.Add(new TextBlock { Text = $"{report.Label} · {report.Quality}",
                [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize13"), MaxWidth = 320, TextWrapping = TextWrapping.Wrap });
            var button = new Button
            {
                Tag = report.Id, Classes = { "theme" }, Margin = new Thickness(0, 0, 8, 8),
                Content = reportContent
            };
            button.SetValue(AutomationProperties.NameProperty, $"{place.Name}. {StoryDrawing.Label(report.Kind)}. {report.Label}. {report.Quality}. {StoryDrawing.RangeLabel(state.Day, report.Start, report.End)}. Select report.");
            button.SetValue(AutomationProperties.HelpTextProperty, $"Synthetic source: {report.SourceId}.");
            button.Click += (_, _) => state.SelectInterval(report.Id);
            buttons.Children.Add(button);
        }
        stack.Children.Add(reports.Length == 0
            ? new TextBlock { Text = "No history supplied. All four tracks are unknown.", Classes = { "muted" }, [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize13") }
            : buttons);
        SizeChanged += (_, _) =>
        {
            foreach (var button in buttons.Children.OfType<Button>()) { button.MaxWidth = Math.Max(0, Bounds.Width); }
        };
        Content = stack;
    }

    public void RefreshSelection() => track.InvalidateVisual();
}

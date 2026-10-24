using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>Native time/offset labels positioned on the same elapsed-day coordinates as timeline tracks.</summary>
public sealed class TimelineRuler : Panel
{
    public IReadOnlyList<DateTimeOffset> TickInstants { get; }

    public TimelineRuler(StoryDay day)
    {
        TickInstants = Array.AsReadOnly(Enumerable.Range(0, 5).Select(index => day.Start.AddTicks((day.End - day.Start).Ticks * index / 4)).ToArray());
        for (var index = 0; index < TickInstants.Count; index++)
        {
            var label = day.TimeLabel(TickInstants[index]);
            var content = new StackPanel { Spacing = 0 };
            var tick = new Avalonia.Controls.Shapes.Line { StartPoint = new Point(0, 0), EndPoint = new Point(0, 4),
                Width = 1, Height = 4, StrokeThickness = 1, Margin = new Thickness(0, 0, 0, 2),
                HorizontalAlignment = index == 0 ? HorizontalAlignment.Left : index == 4 ? HorizontalAlignment.Right : HorizontalAlignment.Center };
            tick[!Avalonia.Controls.Shapes.Line.StrokeProperty] = new DynamicResourceExtension("MutedBrush");
            content.Children.Add(tick);
            content.Children.Add(new TextBlock { Text = label[..5], [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize12"), Classes = { "muted" }, TextAlignment = TextAlignment.Center });
            content.Children.Add(new TextBlock { Text = label[6..], [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize12"), Classes = { "muted" }, TextAlignment = TextAlignment.Center });
            if (index == 4)
            {
                content.Children.Add(new TextBlock { Text = "next day", [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize12"), Classes = { "muted" }, TextAlignment = TextAlignment.Center });
            }
            Children.Add(content);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 326;
        foreach (var child in Children) { child.Measure(new Size(Math.Max(0, width / 4), double.PositiveInfinity)); }
        var sparse = Children.Select(child => child.DesiredSize.Width).DefaultIfEmpty(0).Max() > width / 6;
        Children[1].IsVisible = Children[3].IsVisible = !sparse;
        foreach (var child in Children) { child.Measure(new Size(Math.Max(0, width / 4), double.PositiveInfinity)); }
        return new Size(width, Children.Select(child => child.DesiredSize.Height).DefaultIfEmpty(0).Max());
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var index = 0; index < Children.Count; index++)
        {
            var child = Children[index];
            var width = Math.Min(child.DesiredSize.Width, Math.Max(0, finalSize.Width - 4));
            var point = 2 + Math.Max(0, finalSize.Width - 4) * index / 4;
            var minimum = Math.Min(2, finalSize.Width);
            var x = Math.Clamp(point - width / 2, minimum, Math.Max(minimum, finalSize.Width - 2 - width));
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
        }
        return finalSize;
    }

}

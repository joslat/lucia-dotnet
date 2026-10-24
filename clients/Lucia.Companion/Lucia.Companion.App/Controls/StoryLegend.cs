using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace Lucia.Companion.App.Controls;

public sealed class StoryLegend : UserControl
{
    public StoryLegend()
    {
        var key = new WrapPanel { Orientation = Orientation.Horizontal };
        Add("Light report", "AccentBrush", Geometry.Parse("M0,8 L18,8"));
        Add("Door / gate", "TextBrush", Geometry.Parse("M9,3 L14,8 9,13 4,8 Z"));
        Add("Motion report", "SageBrush", new EllipseGeometry(new Rect(5, 4, 8, 8)));
        Add("Lucia request", "InfoBrush", Geometry.Parse("M9,2 L9,14 M3,8 L15,8"));
        Add("No data", "MutedBrush", Geometry.Parse("M0,8 L18,8"), dotted: true);
        Add("Last-known", "MutedBrush", Geometry.Parse("M0,8 L18,8"), dashed: true);
        Content = key;

        void Add(string label, string brush, Geometry geometry, bool dotted = false, bool dashed = false)
        {
            var mark = new Avalonia.Controls.Shapes.Path { Data = geometry, Width = 18, Height = 16, StrokeThickness = 1.7,
                VerticalAlignment = VerticalAlignment.Center };
            mark[!Avalonia.Controls.Shapes.Path.StrokeProperty] = new DynamicResourceExtension(brush);
            if (dotted) { mark.StrokeDashArray = new AvaloniaList<double> { 1, 3 }; }
            if (dashed) { mark.StrokeDashArray = new AvaloniaList<double> { 3, 2 }; }
            var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 0, 16, 6) };
            item.Children.Add(mark);
            item.Children.Add(new TextBlock { Text = label, [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize13"), Classes = { "muted" } });
            key.Children.Add(item);
        }
    }
}

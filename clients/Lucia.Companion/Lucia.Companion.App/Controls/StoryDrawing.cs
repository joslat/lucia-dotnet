using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>Shared visual grammar only. Time mapping remains in the pure StoryDay model.</summary>
internal static class StoryDrawing
{
    public static IBrush Brush(Control control, string key) => (IBrush)control.FindResource(control.ActualThemeVariant, key)!;
    public static IBrush ForKind(Control control, StoryKind kind) => Brush(control, kind switch
    {
        StoryKind.Lighting => "AccentBrush", StoryKind.Motion => "SageBrush", StoryKind.Lucia => "InfoBrush", _ => "TextBrush"
    });
    public static string Label(StoryKind kind) => kind switch
    {
        StoryKind.Lighting => "Lighting", StoryKind.Contact => "Door / gate", StoryKind.Motion => "Motion report", _ => "Lucia request"
    };
    public static string RangeLabel(StoryDay day, DateTimeOffset start, DateTimeOffset end) =>
        $"{day.TimeLabel(start)} to {day.TimeLabel(end)}{(end == day.End ? " next day" : "")}";
    public static Point OnCircle(Point center, double radius, double fraction) =>
        center + new Vector(Math.Sin(fraction * Math.Tau) * radius, -Math.Cos(fraction * Math.Tau) * radius);

    public static void Arc(DrawingContext context, Point center, double radius, double from, double to, IPen pen)
    {
        if (to <= from || radius <= 0) { return; }
        var path = new StreamGeometry();
        using (var geometry = path.Open())
        {
            geometry.BeginFigure(OnCircle(center, radius, from), false);
            var steps = Math.Max(2, (int)Math.Ceiling((to - from) * 180));
            for (var index = 1; index <= steps; index++)
            {
                geometry.LineTo(OnCircle(center, radius, from + (to - from) * index / steps));
            }
            geometry.EndFigure(false);
        }
        context.DrawGeometry(null, pen, path);
    }

    public static void Marker(DrawingContext context, Point point, StoryKind kind, IBrush brush, double radius = 3)
    {
        if (kind == StoryKind.Contact)
        {
            var path = new StreamGeometry();
            using (var geometry = path.Open())
            {
                geometry.BeginFigure(point + new Vector(0, -radius), true);
                geometry.LineTo(point + new Vector(radius, 0));
                geometry.LineTo(point + new Vector(0, radius));
                geometry.LineTo(point + new Vector(-radius, 0));
                geometry.EndFigure(true);
            }
            context.DrawGeometry(brush, null, path);
        }
        else if (kind == StoryKind.Lucia)
        {
            var pen = new Pen(brush, 1.7);
            context.DrawLine(pen, point + new Vector(-radius, 0), point + new Vector(radius, 0));
            context.DrawLine(pen, point + new Vector(0, -radius), point + new Vector(0, radius));
        }
        else { context.DrawEllipse(brush, null, point, radius, radius); }
    }
}

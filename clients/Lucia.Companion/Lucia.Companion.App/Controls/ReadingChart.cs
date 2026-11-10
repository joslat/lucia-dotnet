using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>
/// One reading across one day. The line stops at the last reading; when the source has gone quiet the time after it is
/// hatched and labelled "No data" rather than extended. The chart is decorative for assistive technology: its accessible
/// name carries the same facts in words.
/// </summary>
public sealed class ReadingChart : Control
{
    private const double LeftAxis = 34;
    private const double BottomAxis = 22;
    private DateTimeOffset dayStart;
    private DateTimeOffset dayEnd;
    private DateTimeOffset now;
    private string unit = string.Empty;

    public IReadOnlyList<ReadingPoint> Points { get; private set; } = [];
    /// <summary>True when the reading stopped before the report time and the remainder is drawn as a gap.</summary>
    public bool HasGap { get; private set; }

    public ReadingChart()
    {
        Height = 180;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public void SetSeries(IReadOnlyList<ReadingPoint> points, DateTimeOffset start, DateTimeOffset end, DateTimeOffset reportTime, string valueUnit, string title, bool sourceIsCurrent)
    {
        ArgumentNullException.ThrowIfNull(points);
        Points = points;
        dayStart = start;
        dayEnd = end;
        now = reportTime;
        unit = valueUnit;
        HasGap = !sourceIsCurrent && points.Count > 0 && points[^1].At < reportTime;
        if (points.Count > 0)
        {
            var last = points[^1];
            var lowest = points.Min(point => point.Value);
            var highest = points.Max(point => point.Value);
            AutomationProperties.SetName(this, $"Sample chart of {title} today, from {Format(lowest)} to {Format(highest)} {unit}. " +
                $"Last reading {Format(last.Value)} {unit} at {last.At.UtcDateTime:HH:mm} UTC." + (HasGap ? " No data after that." : string.Empty));
        }
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Points.Count == 0 || Bounds.Width <= LeftAxis + 8) { return; }
        var plot = new Rect(LeftAxis, 6, Bounds.Width - LeftAxis - 6, Bounds.Height - BottomAxis - 6);
        var grid = Brush("GraphLineBrush");
        var muted = Brush("MutedBrush");
        var line = Brush("InfoBrush");
        var text = Brush("TextBrush");
        var labelSize = Resource("TextSize12") is double size ? size : 12;
        var typeface = new Typeface(Resource("BodyFont") as FontFamily ?? FontFamily.Default);

        var (low, high) = Range();
        double X(DateTimeOffset at) => plot.Left + plot.Width * Math.Clamp((at - dayStart) / (dayEnd - dayStart), 0, 1);
        double Y(double value) => plot.Bottom - plot.Height * (value - low) / (high - low);

        var gridPen = new Pen(grid, 1);
        for (var step = 0; step <= 2; step++)
        {
            var value = low + (high - low) * step / 2;
            context.DrawLine(gridPen, new Point(plot.Left, Y(value)), new Point(plot.Right, Y(value)));
            DrawText(context, Format(value), new Point(0, Y(value) - labelSize * 0.7), typeface, labelSize, muted);
        }
        for (var hour = 0; hour <= 24; hour += 6)
        {
            var x = plot.Left + plot.Width * hour / 24;
            DrawText(context, hour.ToString("00", CultureInfo.InvariantCulture), new Point(x - labelSize * 0.6, plot.Bottom + 4), typeface, labelSize, muted);
        }

        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            stream.BeginFigure(new Point(X(Points[0].At), plot.Bottom), true);
            foreach (var point in Points) { stream.LineTo(new Point(X(point.At), Y(point.Value))); }
            stream.LineTo(new Point(X(Points[^1].At), plot.Bottom));
            stream.EndFigure(true);
        }
        context.DrawGeometry(new SolidColorBrush(line is ISolidColorBrush solid ? solid.Color : Colors.SteelBlue, 0.14), null, geometry);
        var linePen = new Pen(line, 2, lineJoin: PenLineJoin.Round);
        for (var index = 1; index < Points.Count; index++)
        {
            context.DrawLine(linePen, new Point(X(Points[index - 1].At), Y(Points[index - 1].Value)), new Point(X(Points[index].At), Y(Points[index].Value)));
        }

        var last = new Point(X(Points[^1].At), Y(Points[^1].Value));
        context.DrawLine(new Pen(text, 1.5), new Point(X(now), plot.Top), new Point(X(now), plot.Bottom));
        if (HasGap)
        {
            var gap = new Rect(last.X, plot.Top, Math.Max(0, X(now) - last.X), plot.Height);
            using (context.PushClip(gap))
            {
                using (context.PushOpacity(0.35))
                {
                    var hatch = new Pen(muted, 1);
                    for (var x = gap.Left - gap.Height; x < gap.Right; x += 9) { context.DrawLine(hatch, new Point(x, gap.Bottom), new Point(x + gap.Height, gap.Top)); }
                }
            }
            var label = new FormattedText("No data", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, labelSize, text);
            var labelAt = new Point(last.X + 8, plot.Top + plot.Height / 2 - label.Height / 2);
            context.DrawRectangle(Brush("PanelBrush"), null, new Rect(labelAt.X - 3, labelAt.Y - 1, label.Width + 6, label.Height + 2), 4, 4);
            context.DrawText(label, labelAt);
        }
        context.DrawEllipse(Brush("PanelBrush"), linePen, last, 4.5, 4.5);
    }

    private (double Low, double High) Range()
    {
        var low = Points.Min(point => point.Value);
        var high = Points.Max(point => point.Value);
        var padding = Math.Max(1, (high - low) * 0.25);
        low = Math.Floor(low - padding);
        high = Math.Ceiling(high + padding);
        return high - low < 2 ? (low - 1, high + 1) : (low, high);
    }

    private IBrush Brush(string key) => Resource(key) as IBrush ?? Brushes.Gray;
    private object? Resource(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) ? value : null;
    private static string Format(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    private static void DrawText(DrawingContext context, string value, Point origin, Typeface typeface, double size, IBrush brush) =>
        context.DrawText(new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, brush), origin);
}

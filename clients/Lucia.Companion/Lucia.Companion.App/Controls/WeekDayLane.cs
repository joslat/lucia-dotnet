using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>
/// One day's reports on a single 24-hour line: recorded spans as bars, short reports as markers in their kind's colour,
/// last-known spans dashed and unavailable spans in the muted colour. Time with no report stays an empty track.
/// </summary>
public sealed class WeekDayLane : Control
{
    private StoryDay? day;
    private IReadOnlyList<StoryInterval> intervals = [];

    public WeekDayLane()
    {
        Height = 18;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public void SetDay(StoryDay value, IReadOnlyList<StoryInterval> shown)
    {
        day = value;
        intervals = shown;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (day is null || Bounds.Width <= 0) { return; }
        var middle = Bounds.Height / 2;
        context.DrawLine(new Pen(StoryDrawing.Brush(this, "GraphLineBrush"), 4, lineCap: PenLineCap.Round), new Point(2, middle), new Point(Bounds.Width - 2, middle));
        foreach (var interval in intervals)
        {
            var from = 2 + (Bounds.Width - 4) * day.Fraction(interval.Start);
            var to = 2 + (Bounds.Width - 4) * day.Fraction(interval.End);
            var brush = interval.Quality == StoryQuality.Recorded ? StoryDrawing.ForKind(this, interval.Kind) : StoryDrawing.Brush(this, "MutedBrush");
            if (to - from < 4)
            {
                StoryDrawing.Marker(context, new Point(from, middle), interval.Kind, brush, 3.5);
                continue;
            }
            var pen = new Pen(brush, interval.Quality == StoryQuality.Recorded ? 6 : 2, lineCap: PenLineCap.Round)
            {
                DashStyle = interval.Quality == StoryQuality.LastKnown ? new DashStyle([3, 2], 0) : interval.Quality == StoryQuality.Unavailable ? new DashStyle([1, 2], 0) : null,
            };
            context.DrawLine(pen, new Point(from, middle), new Point(to, middle));
        }
    }
}

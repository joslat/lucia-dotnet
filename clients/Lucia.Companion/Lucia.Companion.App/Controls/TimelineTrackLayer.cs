using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>Four distinct evidence tracks. Gaps in one track cannot borrow coverage from another.</summary>
internal sealed class TimelineTrackLayer : Control
{
    public StoryViewModel? State { get; set; }
    public string PlaceId { get; set; } = "";
    public event Action<double>? TimeInvoked;

    public TimelineTrackLayer()
    {
        Height = 64;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
        PointerPressed += (_, args) =>
        {
            if (Bounds.Width > 0 && args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                TimeInvoked?.Invoke(Math.Clamp(args.GetPosition(this).X / Bounds.Width, 0, 1));
                args.Handled = true;
            }
        };
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (State is not { } state || Bounds.Width <= 0) { return; }
        var width = Math.Max(0, Bounds.Width - 4);
        foreach (var kind in Enum.GetValues<StoryKind>())
        {
            var y = 8 + (int)kind * 14;
            context.DrawLine(new Pen(StoryDrawing.Brush(this, "GraphLineBrush"), 2, new DashStyle([1, 3], 0)),
                new Point(2, y), new Point(width + 2, y));
            foreach (var interval in state.Day.Intervals.Where(interval => interval.PlaceId == PlaceId && interval.Kind == kind))
            {
                var from = 2 + state.Day.Fraction(interval.Start) * width;
                var to = 2 + state.Day.Fraction(interval.End) * width;
                var brush = interval.Quality == StoryQuality.Recorded ? StoryDrawing.ForKind(this, kind) : StoryDrawing.Brush(this, "MutedBrush");
                var dash = interval.Quality == StoryQuality.Recorded ? null : new DashStyle(interval.Quality == StoryQuality.LastKnown ? [3, 2] : [1, 3], 0);
                context.DrawLine(new Pen(brush, 3, dash), new Point(from, y), new Point(Math.Max(from + 1, to), y));
                if (interval.Quality == StoryQuality.Recorded && kind != StoryKind.Lighting)
                {
                    StoryDrawing.Marker(context, new Point(from, y), kind, brush, 3);
                }
            }
        }
        var cursor = 2 + state.CursorFraction * width;
        context.DrawLine(new Pen(StoryDrawing.Brush(this, "TextBrush"), 1.5), new Point(cursor, 0), new Point(cursor, 60));
    }
}

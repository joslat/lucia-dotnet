using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>Decorative rings with a pointer accelerator. Named chapters, reports and a native slider provide equivalent input.</summary>
public sealed class StoryOrbit : Control
{
    public static readonly StyledProperty<double> CursorFractionProperty =
        AvaloniaProperty.Register<StoryOrbit, double>(nameof(CursorFraction));
    public double CursorFraction { get => GetValue(CursorFractionProperty); set => SetValue(CursorFractionProperty, value); }
    public StoryViewModel? State { get; private set; }
    /// <summary>True when the shown day is still in progress, so the part after now is hatched as not yet known.</summary>
    public bool HasFuture => State?.Day.HasFuture == true;
    public int RenderedPlaceCount => Math.Min(6, State?.VisiblePlaces.Count ?? 0);
    public event Action<double>? TimeInvoked;
    private bool animate;

    static StoryOrbit() => AffectsRender<StoryOrbit>(CursorFractionProperty);

    public StoryOrbit()
    {
        MinWidth = 240;
        MinHeight = 240;
        Cursor = new Cursor(StandardCursorType.Hand);
        ContextHelp.Set(this, "Select time on the dial, or use the time slider, named chapters and reports. Each ring follows one place.");
        DetachedFromVisualTree += (_, _) => { Transitions = null; animate = false; };
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
        SetValue(AutomationProperties.NameProperty, "StoryOrbit. Use the time slider or named chapters to select time.");
    }

    public void SetState(StoryViewModel state, bool animateSelection)
    {
        State = state;
        if (animate != animateSelection)
        {
            animate = animateSelection;
            Transitions = animate ? new Transitions { new DoubleTransition
            {
                Property = CursorFractionProperty, Duration = TimeSpan.FromMilliseconds(280), Easing = new CubicEaseOut()
            }} : null;
        }
        CursorFraction = state.CursorFraction;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs args)
    {
        base.OnPointerPressed(args);
        var position = args.GetPosition(this);
        var point = new Vector(position.X - Bounds.Width / 2, position.Y - Bounds.Height / 2);
        if (point.Length < 64 || !args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { return; }
        var fraction = Math.Atan2(point.X, -point.Y) / Math.Tau;
        TimeInvoked?.Invoke(fraction < 0 ? fraction + 1 : fraction);
        args.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (State is not { } state || Bounds.Width <= 0 || Bounds.Height <= 0) { return; }
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var outer = Math.Max(0, Math.Min(Bounds.Width, Bounds.Height) / 2 - 25);
        var line = StoryDrawing.Brush(this, "GraphLineBrush");
        var text = StoryDrawing.Brush(this, "TextBrush");
        context.DrawEllipse(null, new Pen(line, 1), center, outer + 9, outer + 9);
        // Hour positions use elapsed instants, including repeated/skipped local hours.
        for (var time = state.Day.Start; time < state.Day.End; time = time.AddHours(1))
        {
            var fraction = state.Day.Fraction(time);
            context.DrawLine(new Pen(line, 1), StoryDrawing.OnCircle(center, outer + 13, fraction),
                StoryDrawing.OnCircle(center, outer + 18, fraction));
        }
        var places = state.VisiblePlaces.Take(6).ToArray();
        var spacing = places.Length <= 1 ? 14 : Math.Min(14, (outer - 68) / (places.Length - 1));
        for (var index = 0; index < places.Length; index++)
        {
            var radius = Math.Max(50, outer - index * spacing);
            var hasReports = state.VisibleIntervals.Any(interval => interval.PlaceId == places[index].Id);
            context.DrawEllipse(null, new Pen(line, 1.5, hasReports ? null : new DashStyle([1, 3], 0)), center, radius, radius);
            foreach (var interval in state.VisibleIntervals.Where(interval => interval.PlaceId == places[index].Id))
            {
                var brush = interval.Quality == StoryQuality.Recorded ? StoryDrawing.ForKind(this, interval.Kind) : StoryDrawing.Brush(this, "MutedBrush");
                var dash = interval.Quality == StoryQuality.Recorded ? null : new DashStyle(interval.Quality == StoryQuality.LastKnown ? [3, 2] : [1, 3], 0);
                var from = state.Day.Fraction(interval.Start);
                var to = state.Day.Fraction(interval.End);
                StoryDrawing.Arc(context, center, radius, from, to, new Pen(brush, interval.Kind == StoryKind.Lighting ? 3 : 5, dash));
                if (interval.Quality == StoryQuality.Recorded && interval.Kind != StoryKind.Lighting)
                {
                    StoryDrawing.Marker(context, StoryDrawing.OnCircle(center, radius, from), interval.Kind, brush, 4);
                }
            }
        }
        if (state.SelectedChapterId is { } chapterId)
        {
            var chapter = state.Day.Chapters.Single(chapter => chapter.Id == chapterId);
            StoryDrawing.Arc(context, center, outer + 9, state.Day.Fraction(chapter.Start), state.Day.Fraction(chapter.End),
                new Pen(StoryDrawing.Brush(this, "AccentBrush"), 2));
        }
        if (state.Day.Now is { } now && state.Day.HasFuture)
        {
            // The rest of today is not yet known: hatch the rings from now to midnight and mark now on the rim.
            var from = state.Day.Fraction(now);
            var inner = Math.Max(50, outer - (Math.Max(1, places.Length) - 1) * spacing) - 6;
            var wedge = new StreamGeometry();
            using (var stream = wedge.Open())
            {
                stream.BeginFigure(StoryDrawing.OnCircle(center, outer + 6, from), true);
                const int steps = 48;
                for (var step = 1; step <= steps; step++) { stream.LineTo(StoryDrawing.OnCircle(center, outer + 6, from + (1 - from) * step / steps)); }
                for (var step = steps; step >= 0; step--) { stream.LineTo(StoryDrawing.OnCircle(center, inner, from + (1 - from) * step / steps)); }
                stream.EndFigure(true);
            }
            using (context.PushGeometryClip(wedge))
            using (context.PushOpacity(0.35))
            {
                var hatch = new Pen(StoryDrawing.Brush(this, "MutedBrush"), 1);
                for (var x = -Bounds.Height; x < Bounds.Width; x += 9) { context.DrawLine(hatch, new Point(x, Bounds.Height), new Point(x + Bounds.Height, 0)); }
            }
            context.DrawLine(new Pen(StoryDrawing.Brush(this, "AccentBrush"), 2), StoryDrawing.OnCircle(center, inner, from), StoryDrawing.OnCircle(center, outer + 18, from));
        }
        var end = StoryDrawing.OnCircle(center, outer + 15, CursorFraction);
        context.DrawLine(new Pen(text, 1.5), center, end);
        context.DrawEllipse(text, null, end, 3, 3);
        context.DrawEllipse(StoryDrawing.Brush(this, "CanvasBrush"), new Pen(line, 1), center, 59, 59);
    }
}

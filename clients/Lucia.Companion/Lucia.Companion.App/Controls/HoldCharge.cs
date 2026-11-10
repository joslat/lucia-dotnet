using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lucia.Companion.App.Controls;

/// <summary>A single charge arc during a pending hold. It does not represent household activity or run its own clock.</summary>
public sealed class HoldCharge : Control
{
    public static readonly StyledProperty<double> ProgressProperty = AvaloniaProperty.Register<HoldCharge, double>(nameof(Progress));
    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    static HoldCharge() => AffectsRender<HoldCharge>(ProgressProperty);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Progress <= 0 || Bounds.Width <= 0 || Bounds.Height <= 0) { return; }
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = Math.Max(0, Math.Min(center.X, center.Y) - 2);
        var pen = new Pen(StoryDrawing.Brush(this, "AccentBrush"), 2.5, lineCap: PenLineCap.Round);
        var fraction = new CubicEaseInOut().Ease(Math.Clamp(Progress, 0, 1));
        if (fraction >= 1) { context.DrawEllipse(null, pen, center, radius, radius); return; }
        var angle = fraction * Math.Tau;
        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            stream.BeginFigure(new Point(center.X, center.Y - radius), false);
            stream.ArcTo(new Point(center.X + Math.Sin(angle) * radius, center.Y - Math.Cos(angle) * radius),
                new Size(radius, radius), 0, fraction > .5, SweepDirection.Clockwise);
            stream.EndFigure(false);
        }
        context.DrawGeometry(null, pen, geometry);
    }
}

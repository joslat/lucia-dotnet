using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>Decorative only. Physical connections are also named in the accessible detail view.</summary>
public sealed class ConnectionLayer : Control
{
    public static readonly StyledProperty<IBrush?> ConnectionBrushProperty =
        AvaloniaProperty.Register<ConnectionLayer, IBrush?>(nameof(ConnectionBrush));
    private IReadOnlyList<PlaceConnection> connections = [];
    private IReadOnlyDictionary<string, Point> anchors = new Dictionary<string, Point>();
    public IBrush? ConnectionBrush { get => GetValue(ConnectionBrushProperty); set => SetValue(ConnectionBrushProperty, value); }

    static ConnectionLayer() => AffectsRender<ConnectionLayer>(ConnectionBrushProperty);
    public void SetConnections(IReadOnlyList<PlaceConnection> value) { connections = value; InvalidateVisual(); }
    public void SetAnchors(IReadOnlyDictionary<string, Point> value) { anchors = value; InvalidateVisual(); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (ConnectionBrush is not { } brush) { return; }
        using (context.PushOpacity(0.28))
        {
            for (var x = 12.0; x < Bounds.Width; x += 24)
            {
                for (var y = 12.0; y < Bounds.Height; y += 24)
                {
                    context.DrawEllipse(brush, null, new Point(x, y), 0.6, 0.6);
                }
            }
        }

        var pen = new Pen(brush, 1);
        foreach (var edge in connections)
        {
            if (!anchors.TryGetValue(edge.FromPlaceId, out var start) || !anchors.TryGetValue(edge.ToPlaceId, out var end)) { continue; }
            var direction = new Vector(end.X - start.X, end.Y - start.Y);
            var length = direction.Length;
            if (length <= 80) { continue; }
            var inset = direction / length * 40;
            context.DrawLine(pen, start + inset, end - inset);
        }
    }
}

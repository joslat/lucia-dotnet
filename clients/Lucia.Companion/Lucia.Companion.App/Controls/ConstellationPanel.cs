using Avalonia;
using Avalonia.Controls;

namespace Lucia.Companion.App.Controls;

/// <summary>Stable width-based slots, not a physics simulation. Native child buttons retain input and automation.</summary>
public sealed class ConstellationPanel : Panel
{
    public event Action<IReadOnlyDictionary<string, Point>>? AnchorsChanged;
    private int columns = 3;
    private double rowHeight = 160;
    private double textScale = 1;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 600;
        textScale = Children.OfType<PlaceOrb>().Select(orb => orb.FontSize / 16).DefaultIfEmpty(1).Max();
        columns = Math.Clamp((int)(width / (108 * textScale)), 1, Math.Max(1, Math.Min(Children.Count, width >= 800 ? 4 : 3)));
        var cellWidth = width / columns;
        rowHeight = 148;
        foreach (var child in Children)
        {
            child.Measure(new Size(Math.Max(0, cellWidth - 8), double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height + 8);
        }

        return new Size(width, Children.Count == 0 ? 0 : Math.Ceiling((double)Children.Count / columns) * rowHeight + (Children.Count <= 6 ? 20 : 24));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var cellWidth = finalSize.Width / columns;
        for (var index = 0; index < Children.Count; index++)
        {
            var column = index % columns;
            var child = Children[index];
            // Android can briefly arrange a zero-width surface before its first viewport.
            var width = Math.Clamp(cellWidth - 8, 0, 152 * textScale);
            // Stable display offsets are not floor-plan coordinates or inferred room classifications.
            double[] lateral = [-0.35, 0.12, 0.35, -0.12, 0.35, -0.3];
            double[] vertical = [8, 0, 20, 0, 20, 12];
            var compactComposition = Children.Count <= 6;
            var x = column * cellWidth + (cellWidth - width) / 2 +
                (compactComposition ? lateral[index % lateral.Length] * Math.Max(0, cellWidth - width - 8) : 0);
            var y = index / columns * rowHeight + (compactComposition ? vertical[index % vertical.Length] : column % 2 == 1 ? 24 : 0);
            child.Arrange(new Rect(x, y, width, child.DesiredSize.Height));
        }

        AnchorsChanged?.Invoke(Children.OfType<PlaceOrb>().Where(orb => orb.Tag is string)
            .ToDictionary(orb => (string)orb.Tag!, orb => new Point(orb.Bounds.Center.X, orb.Bounds.Y + 48), StringComparer.Ordinal));
        return finalSize;
    }
}

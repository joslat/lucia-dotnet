using Avalonia;
using Avalonia.Controls;

namespace Lucia.Companion.App.Controls;

/// <summary>
/// Stable width-based slots, not a physics simulation. Native child buttons retain input and automation.
/// A <see cref="TextBlock"/> child is a zone label for the places that follow it. A zone continues on the current row when all
/// of its places fit there, otherwise it starts a new row; its label sits above its first place. Rows reserve a label band
/// only when the panel has labels, so a home without zones lays out exactly as before.
/// </summary>
public sealed class ConstellationPanel : Panel
{
    private const double LabelGap = 2;
    // Stable display offsets are not floor-plan coordinates or inferred room classifications.
    private static readonly double[] Lateral = [-0.35, 0.12, 0.35, -0.12, 0.35, -0.3];
    private static readonly double[] Vertical = [8, 0, 20, 0, 20, 12];
    public event Action<IReadOnlyDictionary<string, Point>>? AnchorsChanged;
    private int columns = 3;
    private double rowHeight = 160;
    private double labelBand;
    private double textScale = 1;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 600;
        var places = Children.Where(child => child is not TextBlock).ToArray();
        textScale = Children.OfType<PlaceOrb>().Select(orb => orb.FontSize / 16).DefaultIfEmpty(1).Max();
        columns = Math.Clamp((int)(width / (108 * textScale)), 1, Math.Max(1, Math.Min(places.Length, width >= 800 ? 4 : 3)));
        var cellWidth = width / columns;
        rowHeight = 148;
        labelBand = 0;
        foreach (var child in Children)
        {
            if (child is TextBlock)
            {
                child.Measure(new Size(Math.Max(0, cellWidth * columns), double.PositiveInfinity));
                labelBand = Math.Max(labelBand, child.DesiredSize.Height + LabelGap);
                continue;
            }
            // Measure at the width the place will be arranged at, so wrapped text is never cut off.
            child.Measure(new Size(Math.Clamp(cellWidth - 8, 0, 152 * textScale), double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height + 8);
        }

        if (Children.Count == 0) { return new Size(width, 0); }
        return new Size(width, Layout(width, arrange: false) + (places.Length <= 6 ? 20 : 24));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Layout(finalSize.Width, arrange: true);
        AnchorsChanged?.Invoke(Children.OfType<PlaceOrb>().Where(orb => orb.Tag is string)
            .ToDictionary(orb => (string)orb.Tag!, orb => new Point(orb.Bounds.Center.X, orb.Bounds.Y + 48), StringComparer.Ordinal));
        return finalSize;
    }

    /// <summary>Places labels and places in rows and returns the content height.</summary>
    private double Layout(double width, bool arrange)
    {
        var cellWidth = width / columns;
        var compactComposition = Children.Count(child => child is not TextBlock) <= 6;
        var rowStride = labelBand + rowHeight;
        var row = 0;
        var column = 0;
        var placeIndex = 0;
        foreach (var (label, members) in Groups())
        {
            if (column > 0 && members.Count > columns - column) { row++; column = 0; }
            if (label is not null && arrange)
            {
                var labelWidth = Math.Max(0, Math.Min(label.DesiredSize.Width, width - column * cellWidth));
                label.Arrange(new Rect(column * cellWidth, row * rowStride, labelWidth, label.DesiredSize.Height));
            }
            foreach (var child in members)
            {
                // Android can briefly arrange a zero-width surface before its first viewport.
                var childWidth = Math.Clamp(cellWidth - 8, 0, 152 * textScale);
                var x = column * cellWidth + (cellWidth - childWidth) / 2 +
                    (compactComposition ? Lateral[placeIndex % Lateral.Length] * Math.Max(0, cellWidth - childWidth - 8) : 0);
                var y = row * rowStride + labelBand + (compactComposition ? Vertical[placeIndex % Vertical.Length] : column % 2 == 1 ? 24 : 0);
                if (arrange) { child.Arrange(new Rect(x, y, childWidth, child.DesiredSize.Height)); }
                placeIndex++;
                column++;
                if (column == columns) { row++; column = 0; }
            }
        }
        return (column > 0 ? row + 1 : row) * rowStride;
    }

    private List<(TextBlock? Label, List<Control> Members)> Groups()
    {
        var groups = new List<(TextBlock? Label, List<Control> Members)>();
        foreach (var child in Children)
        {
            if (child is TextBlock label) { groups.Add((label, [])); continue; }
            if (groups.Count == 0) { groups.Add((null, [])); }
            groups[^1].Members.Add(child);
        }
        return groups;
    }
}

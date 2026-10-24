using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Lucia.Companion.App.Controls;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class ConstellationPanelTests
{
    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    public void TransientStartupWidthCannotProduceNegativeArrangeRectangle(double width)
    {
        var panel = new ConstellationPanel();
        for (var index = 0; index < 6; index++) { panel.Children.Add(new Button { Content = $"Place {index}" }); }
        panel.Measure(new Size(390, double.PositiveInfinity));
        panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height));
        Assert.All(panel.Children, child => Assert.True(double.IsFinite(child.Bounds.Width) && child.Bounds.Width >= 0));

        // Android can arrange a zero-sized surface before its first measured viewport.
        panel.InvalidateMeasure();
        panel.Measure(new Size(390, double.PositiveInfinity));
        panel.Arrange(new Rect(0, 0, 390, panel.DesiredSize.Height));
        Assert.All(panel.Children, child => Assert.InRange(child.Bounds.Width, 48, 152));
    }
}

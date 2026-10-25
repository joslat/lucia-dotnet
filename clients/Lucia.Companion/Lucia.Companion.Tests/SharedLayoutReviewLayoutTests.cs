using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Configuration;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class SharedLayoutReviewLayoutTests
{
    [AvaloniaTheory]
    [InlineData(390, false, 1)]
    [InlineData(390, true, 1)]
    [InlineData(1194, false, 1)]
    [InlineData(1194, true, 1)]
    [InlineData(390, false, 2)]
    [InlineData(390, true, 2)]
    [InlineData(1194, false, 2)]
    [InlineData(1194, true, 2)]
    public async Task ReviewDetailsAndUnavailableStatesRemainReadableAtPhoneTabletAndLargeText(int width, bool dark, int scale)
    {
        CompanionTypography.Apply(size => size * scale);
        var original = SharedLayoutAdoptionControllerTests.Draft();
        var identity = new ConnectionSession(new Uri("https://synthetic.invalid/"), new DeviceSession(new string('a', 32), "Phone", DateTimeOffset.UtcNow.AddDays(1), "home-a", "member-a",
            [new("porch", [ObservationRole.Temperature]), new("garden", [ObservationRole.Motion])]));
        var layout = new SharedHomeLayout("home-a", "member-a", new string('A', 64), DateTimeOffset.UtcNow, "Our home, from porch to garden", true,
            [new("front", "Front porch beside the garden and side entrance", PlaceIcon.Door, "porch"), new("garden", "Garden", PlaceIcon.Garden, "garden")],
            [new("front", "garden")], [new("front", "porch", "sensor." + new string('a', 225), ObservationRole.Temperature)]);
        var service = SharedLayoutAdoptionControllerTests.Service((_, _) => Task.FromResult<SharedHomeLayout?>(layout));
        using var controller = new SharedLayoutAdoptionController(service, identity, () => original);
        using var screen = new HomeSharedLayoutScreen(controller);
        var window = new Window { Width = width, Height = 900, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light, Content = screen }; window.Show();
        var prefix = $"shared-review-{width}-{(dark ? "dark" : "light")}-text{scale}";
        try
        {
            await controller.PendingOperation; Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.NotNull(controller.Plan);
            var expanded = width >= 900 && scale == 1;
            Assert.Equal(expanded ? 1 : 0, Grid.GetColumn(screen.FindControl<Border>("SharedAlternative")!));
            AssertTextWidths(screen, window);
            NativeCapture.Save(window, prefix + "-review.png");
            var summary = screen.FindControl<HomeLayoutSummary>("SharedLayoutSummary")!;
            var details = summary.GetVisualDescendants().OfType<Expander>().First(); details.IsExpanded = true;
            window.UpdateLayout(); details.BringIntoView(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            AssertTextWidths(screen, window);
            Assert.Contains(summary.GetVisualDescendants().OfType<TextBlock>(), text => text.Text?.Contains("sensor.", StringComparison.Ordinal) == true);
            NativeCapture.Save(window, prefix + "-details.png");
            service.SharedLayout = (_, _) => throw new CompanionConnectionException(ConnectionFailure.Rejected, System.Net.HttpStatusCode.ServiceUnavailable);
            await controller.ReadAsync(TestContext.Current.CancellationToken);
            screen.FindControl<ScrollViewer>("SharedReviewScroll")!.Offset = default;
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.Null(controller.Plan); Assert.False(screen.FindControl<Border>("SharedAlternative")!.IsVisible);
            Assert.DoesNotContain(screen.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible), text => text.Text?.Contains("sensor.", StringComparison.Ordinal) == true);
            AssertTextWidths(screen, window);
            NativeCapture.Save(window, prefix + "-unavailable.png");
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    private static void AssertTextWidths(Control screen, Window window)
    {
        foreach (var text in screen.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible && text.Bounds.Width > 0 && !string.IsNullOrEmpty(text.Text)))
        {
            var point = text.TranslatePoint(default, window)!.Value;
            Assert.True(point.X >= -1 && point.X + text.Bounds.Width <= window.Width + 1, $"Text escapes width: {text.Text}");
            Assert.True(text.TextLayout.Width <= text.Bounds.Width + 1, $"Text does not wrap: {text.Text}");
        }
    }
}

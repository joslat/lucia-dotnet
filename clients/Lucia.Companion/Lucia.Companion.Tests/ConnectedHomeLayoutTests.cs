using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class ConnectedHomeLayoutTests
{
    [AvaloniaTheory]
    [InlineData(390, false, 1.0)]
    [InlineData(390, true, 1.0)]
    [InlineData(1000, false, 1.0)]
    [InlineData(1000, true, 1.0)]
    [InlineData(390, false, 2.0)]
    [InlineData(390, true, 2.0)]
    [InlineData(1000, false, 2.0)]
    [InlineData(1000, true, 2.0)]
    public async Task ImportReviewAndSourceRecoveryRemainReadableAcrossPhoneTabletThemesAndLargeText(int width, bool dark, double scale)
    {
        CompanionTypography.Apply(size => size * scale);
        var suffix = $"{width}-{dark}-{scale:0.0}";
        var catalog = new HomeCatalog("home-a", DateTimeOffset.UtcNow, Enumerable.Range(0, 40).Select(index =>
            new HomePlaceCatalog($"place-{index}", index == 0 ? "Garden, greenhouse and covered outdoor seating" : $"Authorized place {index + 1}",
                [new HomeSource($"sensor.temperature_{index}", "Greenhouse temperature report", ObservationRole.Temperature)])).ToArray());
        var import = new HomeImportScreen(new("home-a", "Our home", [], []), catalog);
        var window = new Window { Width = width, Height = 900, Content = import, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            var row = import.GetVisualDescendants().OfType<HomeImportPlaceRow>().First();
            row.FindControl<CheckBox>("SelectImportPlace")!.IsChecked = true;
            window.UpdateLayout();
            NativeCapture.Save(window, $"import-select-{suffix}.png");
            Click(import, "PreviewImport");
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.True(import.FindControl<Button>("AddImportToDraft")!.IsEnabled);
            Assert.Equal(width >= 900 && scale == 1, import.FindControl<StackPanel>("ImportSelection")!.IsVisible);
            Assert.Equal(width >= 900 && scale == 1 ? 1 : 0, Grid.GetColumn(import.FindControl<StackPanel>("ImportReview")!));
            foreach (var name in new[] { "PreviewImport", "AddImportToDraft", "CancelImport" })
            {
                var button = import.FindControl<Button>(name)!;
                var position = button.TranslatePoint(default, window)!.Value;
                Assert.True(button.Bounds.Height >= 48);
                Assert.InRange(position.X, 0, width);
                Assert.True(position.X + button.Bounds.Width <= width + 1);
                Assert.True(position.Y + button.Bounds.Height <= window.Height + 1);
            }
            NativeCapture.Save(window, $"import-review-{suffix}.png");

            var service = ConnectedHomeControllerTests.Service();
            var root = new CompanionRoot(service, connectedStoreFactory: _ => null);
            window.Content = root; window.UpdateLayout(); await root.PendingOperation;
            Click(root.GetVisualDescendants().OfType<ConnectionScreen>().Single(), "OpenConnectedHome");
            await root.PendingOperation; window.UpdateLayout();
            var workspace = root.GetVisualDescendants().OfType<ConnectedHomeWorkspace>().Single();
            service.Home = () => throw new CompanionConnectionException(ConnectionFailure.Rejected, HttpStatusCode.ServiceUnavailable);
            await workspace.RefreshAsync(TestContext.Current.CancellationToken);
            window.UpdateLayout();
            Assert.True(workspace.FindControl<Button>("RetryHomeReports")!.IsEnabled);
            Assert.False(workspace.FindControl<ContentControl>("ConnectedShellHost")!.IsVisible);
            Assert.Contains("source", workspace.Controller.StatusText);
            NativeCapture.Save(window, $"connected-source-recovery-{suffix}.png");
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    private static void Click(Control parent, string name) => parent.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}

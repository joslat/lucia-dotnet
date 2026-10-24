using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class ConnectedActivityLayoutTests
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
    public async Task ReportedRequestsCoverageAndRecoveryRemainReadable(int width, bool dark, double scale)
    {
        CompanionTypography.Apply(size => size * scale);
        var now = DateTimeOffset.UtcNow;
        var requests = new[] { RequestActivityState.Working, RequestActivityState.NeedsInput, RequestActivityState.ResponseReady,
            RequestActivityState.Continued, RequestActivityState.Failed, RequestActivityState.OutcomeUnknown, RequestActivityState.Received }
            .Select((state, index) => new ActivityRequest((index + 1).ToString("x8").PadRight(32, 'a'), state, now.AddMinutes(-2), now.AddSeconds(-20 - index), index == 0 ? 2 : 3)).ToArray();
        var service = ConnectedHomeControllerTests.Service();
        service.Activity = () => Task.FromResult(new ActivitySnapshot("home-a", "member-a", now, now.AddMinutes(-10), true, true, false, requests));
        using var workspace = new ConnectedHomeWorkspace(service, ConnectedActivityWorkspaceTests.Identity(), null);
        var window = new Window { Width = width, Height = 900, Content = workspace, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            await workspace.Shell.ProfileSession.InitializeAsync(TestContext.Current.CancellationToken);
            workspace.State.ReplaceHome(ConnectedHomeControllerTests.Setup(), notifyPreferences: false);
            workspace.State.SetSectionOrder([HomeSection.LuciaNow, HomeSection.Places, HomeSection.Attention]);
            await workspace.RefreshAsync(TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var activity = workspace.GetVisualDescendants().OfType<ConnectedLuciaNow>().Single();
            var suffix = $"{width}-{(dark ? "dark" : "light")}-{scale:0.0}";
            NativeCapture.Save(window, $"activity-home-top-{suffix}.png");
            var more = activity.GetVisualDescendants().OfType<Expander>().Single();
            more.IsExpanded = true; window.UpdateLayout();
            var button = activity.GetVisualDescendants().OfType<Button>().Single(item => item.Name == "RefreshActivity");
            button.BringIntoView(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var point = button.TranslatePoint(default, window)!.Value;
            Assert.True(button.Bounds.Height >= 48);
            Assert.InRange(point.X, 0, width); Assert.True(point.X + button.Bounds.Width <= width + 1);
            Assert.Contains("lost", ConnectedActivityScreenTests.Text(activity));
            NativeCapture.Save(window, $"activity-home-details-{suffix}.png");
            button.Focus();
            service.Activity = () => throw new ActivityAccessException();
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await workspace.ActivityController.PendingOperation;
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.True(workspace.Controller.HasData); Assert.Empty(activity.GetVisualDescendants().OfType<LuciaStagePath>());
            Assert.Contains("not permitted", ConnectedActivityScreenTests.Text(activity));
            var status = activity.GetVisualDescendants().OfType<TextBlock>().Single(item => item.Name == "ActivityStatus");
            Assert.Equal(status, window.FocusManager!.GetFocusedElement());
            AssertInsideClippingViewports(status, window);
            NativeCapture.Save(window, $"activity-home-denied-{suffix}.png");

            // A timer-driven denial must also repair focus when its expanded content disappears.
            service.Activity = () => Task.FromResult(new ActivitySnapshot("home-a", "member-a", now, now.AddMinutes(-10), true, true, false, requests));
            await workspace.ActivityController.RefreshAsync(TestContext.Current.CancellationToken);
            window.UpdateLayout(); more.IsExpanded = true; window.UpdateLayout();
            var toggle = more.GetVisualDescendants().OfType<ToggleButton>().First();
            toggle.BringIntoView(); toggle.Focus();
            service.Activity = () => throw new ActivityAccessException();
            await workspace.ActivityController.RefreshAsync(TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.Equal(status, window.FocusManager.GetFocusedElement());
            AssertInsideClippingViewports(status, window);
            NativeCapture.Save(window, $"activity-home-denied-expanded-{suffix}.png");
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    private static void AssertInsideClippingViewports(Control control, Window window)
    {
        var top = control.TranslatePoint(default, window)!.Value;
        var bottom = top.Y + control.Bounds.Height;
        Assert.InRange(top.Y, 0, window.Height);
        Assert.InRange(bottom, 0, window.Height + 1);
        foreach (var ancestor in control.GetVisualAncestors().Where(item => item.ClipToBounds))
        {
            var origin = ancestor.TranslatePoint(default, window)!.Value;
            Assert.True(top.Y >= origin.Y - 1, $"Status top {top.Y} clipped above {ancestor.GetType().Name} at {origin.Y}.");
            Assert.True(bottom <= origin.Y + ancestor.Bounds.Height + 1, $"Status bottom {bottom} clipped below {ancestor.GetType().Name}.");
        }
    }
}

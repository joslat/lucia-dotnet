using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App.Configuration;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Connection;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class ConnectedActivityScreenTests
{
    [AvaloniaFact]
    public async Task ReportedStagesReplaceOnlyTheirOwnRequestsAndAnUnavailableReadHidesThem()
    {
        var service = ConnectedActivityControllerTests.Service(() => Task.FromResult(ConnectedActivityControllerTests.Snapshot()));
        using var controller = new ConnectedActivityController(service, ConnectedActivityControllerTests.Identity());
        var view = new ConnectedLuciaNow(); view.SetController(controller);
        var window = new Window { Width = 390, Height = 800, Content = view }; window.Show();
        try
        {
            await controller.RefreshAsync(TestContext.Current.CancellationToken); window.UpdateLayout();
            Assert.Single(view.GetVisualDescendants().OfType<LuciaStagePath>());
            Assert.Contains("Working", Text(view));
            Assert.DoesNotContain("Example request", Text(view));
            Assert.Contains("Target area not reported", Text(view));
            service.Activity = () => throw new CompanionConnectionException(ConnectionFailure.Transport);
            await controller.RefreshAsync(TestContext.Current.CancellationToken); window.UpdateLayout();
            Assert.Empty(view.GetVisualDescendants().OfType<LuciaStagePath>());
            Assert.Contains("could not be verified", Text(view));
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(RequestActivityState.Received, "Received")]
    [InlineData(RequestActivityState.Working, "Working")]
    [InlineData(RequestActivityState.NeedsInput, "Reply requested")]
    [InlineData(RequestActivityState.ResponseReady, "Response ready")]
    [InlineData(RequestActivityState.Failed, "Reported failure")]
    [InlineData(RequestActivityState.OutcomeUnknown, "Outcome unknown")]
    [InlineData(RequestActivityState.Continued, "Later turn received")]
    public void EveryReportedStateHasWrittenMeaningWithoutAClaimOfPhysicalSuccess(RequestActivityState state, string caption)
    {
        var view = new LuciaStagePath(); var now = DateTimeOffset.UtcNow;
        view.SetRequest(new(new string('a', 32), state, now.AddSeconds(-2), now, 3));
        var window = new Window { Width = 390, Height = 600, Content = view }; window.Show();
        try
        {
            window.UpdateLayout();
            Assert.Contains(caption, Text(view));
            Assert.DoesNotContain("Action completed", Text(view));
            Assert.Contains("Target area not reported", Text(view));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ADeviceWithoutActivityApprovalShowsItsReasonAndSendsNothing()
    {
        var service = new TestCompanionConnection { Activity = () => throw new InvalidOperationException("Must not send.") };
        using var controller = new ConnectedActivityController(service, TestCompanionConnection.Session());
        var view = new ConnectedLuciaNow(); view.SetController(controller);
        var window = new Window { Width = 390, Height = 800, Content = view }; window.Show();
        try
        {
            await controller.RefreshAsync(TestContext.Current.CancellationToken); window.UpdateLayout();
            Assert.Contains("not approved", Text(view));
            Assert.False(view.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "RefreshActivity").IsEnabled);
            Assert.Empty(view.GetVisualDescendants().OfType<LuciaStagePath>());
        }
        finally { window.Close(); }
    }

    internal static string Text(Control control) => string.Join(" ", control.GetVisualDescendants().OfType<TextBlock>().Where(item => item.IsEffectivelyVisible).Select(item => item.Text));

    [AvaloniaFact]
    public void CategoryAndCurrentAreaRefreshIndependentlyOfTheRequestRevisionAndClearWhenUnqualified()
    {
        var view = new LuciaStagePath(); var now = DateTimeOffset.UtcNow;
        ActivityRequest Request(ActivityTargetAreas? areas) => new(new string('a', 32), RequestActivityState.Working, now.AddSeconds(-2), now, 3,
            category: RequestActivityCategory.Lighting, currentTargetAreas: areas);
        view.SetRequest(Request(new(now, true, [new("porch", "Porch")])));
        var window = new Window { Width = 390, Height = 900, Content = view }; window.Show();
        try
        {
            window.UpdateLayout();
            Assert.Contains("Lighting request", Text(view));
            Assert.Contains("Current target areas: Porch", Text(view));
            Assert.Contains("Some target areas cannot be shown", Text(view));
            view.SetRequest(Request(new(now.AddSeconds(5), false, [new("porch", "Garden porch")]))); window.UpdateLayout();
            Assert.Contains("Current target areas: Garden porch", Text(view));
            Assert.DoesNotContain("Some target areas cannot be shown", Text(view));
            view.SetRequest(Request(null)); window.UpdateLayout();
            Assert.Contains("Target area not reported", Text(view)); Assert.DoesNotContain("Garden porch", Text(view));
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(RequestActivityState.Continued)]
    [InlineData(RequestActivityState.Failed)]
    [InlineData(RequestActivityState.OutcomeUnknown)]
    public void ExceptionalOutcomesDoNotInventAResponsePhase(RequestActivityState state)
    {
        var view = new LuciaStagePath(); var now = DateTimeOffset.UtcNow;
        view.SetRequest(new(new string('a', 32), state, now.AddSeconds(-2), now, 3));
        var window = new Window { Width = 390, Height = 600, Content = view }; window.Show();
        try
        {
            window.UpdateLayout();
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBlock>(), item => item.IsEffectivelyVisible && item.Text == "Response");
            Assert.DoesNotContain(view.GetVisualDescendants(), item => item.IsEffectivelyVisible && item.GetType().Name == "LuciaStageTrack");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LargeTextPhasesReflowWithoutBreakingWordsOrReducingType()
    {
        CompanionTypography.Apply(size => size * 2);
        var view = new LuciaStagePath(); var now = DateTimeOffset.UtcNow;
        view.SetRequest(new(new string('a', 32), RequestActivityState.Working, now.AddSeconds(-2), now, 2));
        var window = new Window { Width = 290, Height = 600, Content = view }; window.Show();
        try
        {
            window.UpdateLayout();
            var labels = view.GetVisualDescendants().OfType<TextBlock>().Where(item => item.Text is "Received" or "Processing" or "Response").ToArray();
            Assert.Equal(3, labels.Length);
            foreach (var label in labels)
            {
                Assert.Equal(26, label.FontSize);
                Assert.Single(label.TextLayout.TextLines);
                Assert.True(label.TextLayout.Width <= label.Bounds.Width + 1);
            }
            var positions = labels.Select(label => label.TranslatePoint(default, window)!.Value).ToArray();
            Assert.True(positions[0].Y < positions[1].Y && positions[1].Y < positions[2].Y);
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    [AvaloniaFact]
    public async Task ASlowExplicitRefreshDoesNotStealFocusAfterTheUserLeavesActivity()
    {
        var service = ConnectedActivityControllerTests.Service(() => Task.FromResult(ConnectedActivityControllerTests.Snapshot()));
        using var controller = new ConnectedActivityController(service, ConnectedActivityControllerTests.Identity());
        var view = new ConnectedLuciaNow(); view.SetController(controller);
        var outside = new Button { Content = "Another Home task" };
        var panel = new StackPanel(); panel.Children.Add(outside); panel.Children.Add(view);
        var window = new Window { Width = 390, Height = 900, Content = panel }; window.Show();
        try
        {
            await controller.RefreshAsync(TestContext.Current.CancellationToken); window.UpdateLayout();
            var completion = new TaskCompletionSource<ActivitySnapshot>();
            service.Activity = () => completion.Task;
            var refresh = view.GetVisualDescendants().OfType<Button>().Single(item => item.Name == "RefreshActivity");
            refresh.Focus(); refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            outside.Focus();
            completion.SetException(new ActivityAccessException());
            await controller.PendingOperation;
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.Equal(outside, window.FocusManager!.GetFocusedElement());
            Assert.Contains("not permitted", Text(view));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task RebindingAPrefetchedMemberCannotReuseAnotherMembersRequestRendering()
    {
        using var first = new ConnectedActivityController(ConnectedActivityControllerTests.Service(() => Task.FromResult(ConnectedActivityControllerTests.Snapshot())), ConnectedActivityControllerTests.Identity());
        var now = DateTimeOffset.UtcNow;
        using var next = new ConnectedActivityController(ConnectedActivityControllerTests.Service(() => Task.FromResult(new ActivitySnapshot("home-a", "member-b", now, now.AddMinutes(-1), true, false, false,
            [new(new string('a', 32), RequestActivityState.Failed, now.AddSeconds(-2), now.AddSeconds(-1), 2)]))),
            new(new Uri("https://synthetic.invalid"), new(new string('b', 32), "Other device", now.AddDays(1), "home-a", "member-b", [], canReadActivity: true)));
        await first.RefreshAsync(TestContext.Current.CancellationToken); await next.RefreshAsync(TestContext.Current.CancellationToken);
        var view = new ConnectedLuciaNow(); view.SetController(first);
        var window = new Window { Width = 390, Height = 900, Content = view }; window.Show();
        try
        {
            window.UpdateLayout(); Assert.Contains("Working", Text(view));
            view.SetController(next); window.UpdateLayout();
            Assert.Contains("Reported failure", Text(view)); Assert.DoesNotContain("Working", Text(view));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LargeTextHomeContextScrollsWithTheActiveViewRatherThanStarvingItsViewport()
    {
        CompanionTypography.Apply(size => size * 2);
        var state = new HomeViewModel(ConnectedHomeControllerTests.Setup(), isDemo: false);
        var view = new HomeScreen(state);
        var window = new Window { Width = 390, Height = 900, Content = view }; window.Show();
        try
        {
            window.UpdateLayout();
            var heading = view.FindControl<TextBlock>("HomeName")!;
            Assert.Contains(view.FindControl<ScrollViewer>("ContextScroll")!, heading.GetVisualAncestors());
            Assert.Equal(ScrollBarVisibility.Auto, view.FindControl<ScrollViewer>("ContextScroll")!.VerticalScrollBarVisibility);
            Assert.Equal(ScrollBarVisibility.Disabled, view.FindControl<ScrollViewer>("HomeScroll")!.VerticalScrollBarVisibility);
            state.SelectPlace("porch"); window.UpdateLayout();
            Assert.Equal(ScrollBarVisibility.Disabled, view.FindControl<ScrollViewer>("DetailScroll")!.VerticalScrollBarVisibility);
            CompanionTypography.Apply(size => size); window.UpdateLayout();
            Assert.Equal(ScrollBarVisibility.Disabled, view.FindControl<ScrollViewer>("ContextScroll")!.VerticalScrollBarVisibility);
            Assert.Equal(ScrollBarVisibility.Auto, view.FindControl<ScrollViewer>("DetailScroll")!.VerticalScrollBarVisibility);
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }
}

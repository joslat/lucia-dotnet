using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class SurfaceEntranceTests
{
    [AvaloniaFact]
    public async Task ArrivalKeepsTextVisibleWhileItsPositionSettlesAndThenStops()
    {
        var panel = new Border { Child = new TextBlock { Text = "Verified reports are ready" } };
        SurfaceEntrance.SetEnabled(panel, true);
        MotionSettings.Current.ReduceMotion = false;
        var window = new Window { Width = 390, Height = 120, Content = panel };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            Assert.NotNull(panel.Transitions);
            Assert.Contains(panel.Transitions, transition => transition is TransformOperationsTransition { Duration.TotalMilliseconds: 220 });
            var offsets = new List<double>();
            for (var index = 0; index < 10; index++)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
                offsets.Add(panel.RenderTransform?.Value.M32 ?? 0);
                Assert.Equal(1, panel.Opacity);
                await Task.Delay(30, TestContext.Current.CancellationToken);
            }
            Assert.Contains(offsets, value => value > 0 && value < 4);
            Assert.Equal(0, offsets[^1], precision: 2);
        }
        finally { MotionSettings.Current.ReduceMotion = true; window.Close(); }
    }

    [AvaloniaFact]
    public void HiddenAncestorsAndMotionChangesSettleImmediatelyWithoutReplaying()
    {
        var child = new Border { Child = new TextBlock { Text = "Ready" } };
        var parent = new Border { Child = child };
        SurfaceEntrance.SetEnabled(child, true);
        var window = new Window { Content = parent };
        try
        {
            window.Show();
            Assert.Null(child.Transitions);
            MotionSettings.Current.ReduceMotion = false;
            Assert.Null(child.Transitions);
            parent.IsVisible = false;
            parent.IsVisible = true;
            Assert.NotNull(child.Transitions);
            parent.IsVisible = false;
            Assert.Null(child.Transitions);
            Assert.Equal(0, child.RenderTransform?.Value.M32 ?? 0);
            parent.IsVisible = true;
            MotionSettings.Current.Pause();
            Assert.Null(child.Transitions);
            MotionSettings.Current.Resume();
            Assert.Null(child.Transitions);
        }
        finally { MotionSettings.Current.ReduceMotion = true; MotionSettings.Current.Resume(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task AnOrdinaryReadyHomeRefreshDoesNotReplayItsArrival()
    {
        MotionSettings.Current.ReduceMotion = false;
        using var workspace = new ConnectedHomeWorkspace(ConnectedHomeControllerTests.Service(), TestCompanionConnection.Session(), store: null);
        var window = new Window { Width = 1194, Height = 834, Content = workspace };
        try
        {
            window.Show();
            await workspace.RefreshAsync(TestContext.Current.CancellationToken);
            var host = workspace.FindControl<ContentControl>("ConnectedShellHost")!;
            Assert.True(SurfaceEntrance.GetEnabled(host));
            var arrival = host.Transitions;
            Assert.NotNull(arrival);
            await workspace.Controller.RefreshAsync(hideWhileChecking: false, TestContext.Current.CancellationToken);
            Assert.True(workspace.Controller.HasData);
            Assert.Same(arrival, host.Transitions);
        }
        finally { MotionSettings.Current.ReduceMotion = true; window.Close(); }
    }
}

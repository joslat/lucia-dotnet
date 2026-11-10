using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.Connection;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class OperationProgressTests
{
    [AvaloniaFact]
    public void ProgressExistsOnlyWhileWorkIsPendingAndNeverClaimsAPercentage()
    {
        var progress = new OperationProgress();
        Assert.False(progress.IsVisible);
        MotionSettings.Current.ReduceMotion = false;
        var window = new Window { Width = 390, Height = 100, Content = progress };
        try
        {
            progress.IsBusy = true;
            window.Show();
            var bar = Assert.Single(progress.GetVisualDescendants().OfType<ProgressBar>());
            Assert.True(progress.IsVisible);
            Assert.True(bar.IsIndeterminate);
            Assert.Equal(0, bar.Value);
            progress.IsBusy = false;
            Assert.False(progress.IsVisible);
            Assert.False(bar.IsIndeterminate);
        }
        finally { MotionSettings.Current.ReduceMotion = true; window.Close(); }
    }

    [AvaloniaFact]
    public void ReducedMotionBackgroundAndHiddenAncestorsStopIndeterminateAnimation()
    {
        var progress = new OperationProgress { IsBusy = true };
        var parent = new Border { Child = progress };
        var window = new Window { Content = parent };
        MotionSettings.Current.ReduceMotion = false;
        try
        {
            window.Show();
            var bar = Assert.Single(progress.GetVisualDescendants().OfType<ProgressBar>());
            Assert.True(bar.IsIndeterminate);
            parent.IsVisible = false;
            Assert.False(bar.IsIndeterminate);
            parent.IsVisible = true;
            Assert.True(bar.IsIndeterminate);
            MotionSettings.Current.ReduceMotion = true;
            Assert.False(bar.IsIndeterminate);
            Assert.Equal(3, progress.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().Count(dot => dot.IsEffectivelyVisible));
            MotionSettings.Current.ReduceMotion = false;
            MotionSettings.Current.Pause();
            Assert.False(bar.IsIndeterminate);
            MotionSettings.Current.Resume();
            Assert.True(bar.IsIndeterminate);
            window.Close();
            Assert.False(bar.IsIndeterminate);
        }
        finally { MotionSettings.Current.ReduceMotion = true; MotionSettings.Current.Resume(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task TheConnectedReadIndicatorFollowsTheActualPendingRequest()
    {
        var service = ConnectedHomeControllerTests.Service();
        var response = new TaskCompletionSource<HomeSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Home = () => response.Task;
        using var workspace = new ConnectedHomeWorkspace(service, TestCompanionConnection.Session(), store: null);
        var window = new Window { Width = 390, Height = 844, Content = workspace };
        Task pending = Task.CompletedTask;
        try
        {
            window.Show();
            pending = workspace.RefreshAsync(TestContext.Current.CancellationToken);
            Assert.True(workspace.Controller.IsBusy);
            var progress = workspace.FindControl<OperationProgress>("HomeOperationProgress");
            Assert.NotNull(progress);
            Assert.True(progress.IsBusy);
            Assert.False(workspace.FindControl<ContentControl>("ConnectedShellHost")!.IsVisible);
            response.SetResult(ConnectedHomeControllerTests.Snapshot());
            await pending;
            Assert.False(progress.IsBusy);
            Assert.True(workspace.Controller.HasData);
            Assert.True(workspace.FindControl<ContentControl>("ConnectedShellHost")!.IsVisible);
            service.Home = () => throw new CompanionConnectionException(ConnectionFailure.Rejected, HttpStatusCode.ServiceUnavailable);
            await workspace.RefreshAsync(TestContext.Current.CancellationToken);
            Assert.False(progress.IsBusy);
            Assert.False(workspace.Controller.HasData);
            Assert.False(workspace.FindControl<ContentControl>("ConnectedShellHost")!.IsVisible);
        }
        finally { response.TrySetResult(ConnectedHomeControllerTests.Snapshot()); await pending; window.Close(); }
    }

    [AvaloniaFact]
    public async Task ConnectionInitializationUsesTheSameActualOperationFeedback()
    {
        var response = new TaskCompletionSource<ConnectionSession?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new TestCompanionConnection { HasSavedConnection = true, Restore = () => response.Task };
        var root = new CompanionRoot(service);
        var window = new Window { Width = 390, Height = 844, Content = root };
        try
        {
            window.Show();
            var screen = Assert.Single(root.GetVisualDescendants().OfType<ConnectionScreen>());
            Assert.True(root.State.IsBusy);
            var progress = screen.FindControl<OperationProgress>("ConnectionOperationProgress");
            Assert.NotNull(progress);
            Assert.True(progress.IsBusy);
            response.SetResult(TestCompanionConnection.Session());
            await root.PendingOperation;
            Assert.False(root.State.IsBusy);
            Assert.False(progress.IsBusy);
        }
        finally { response.TrySetResult(null); await root.PendingOperation; window.Close(); }
    }
}

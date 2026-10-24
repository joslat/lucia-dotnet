using System.Net;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Screens;
using Lucia.Companion.Connection;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class ConnectedHomeRecoveryScreenTests
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
    public async Task AutomaticRecoveryPreservesTheEditorAndThrottleTicksDoNotStealFocus(int width, bool dark, double scale)
    {
        CompanionTypography.Apply(size => size * scale);
        var clock = new MutableConnectionClock();
        var service = ConnectedHomeControllerTests.Service();
        var revision = HomeUpdateClientTests.Revision;
        string? requested = null;
        service.HomeUpdate = cursor => { requested = cursor; return Task.FromResult(new HomeUpdate(revision, ConnectedHomeControllerTests.Snapshot())); };
        using var workspace = new ConnectedHomeWorkspace(service, TestCompanionConnection.Session(), null, clock);
        var window = new Window { Width = width, Height = 900, Content = workspace, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        var suffix = $"{width}-{dark}-{scale:0.0}";
        window.Show();
        try
        {
            await workspace.RefreshAsync(TestContext.Current.CancellationToken);
            workspace.State.ReplaceHome(ConnectedHomeControllerTests.Setup());
            Click(workspace.Shell, "BuildHomeButton"); window.UpdateLayout();
            var editor = workspace.GetVisualDescendants().OfType<HomeEditorScreen>().Single();
            editor.FindControl<TextBox>("HomeNameInput")!.Text = "Our unsaved home name";
            service.HomeUpdate = _ => throw new CompanionConnectionException(ConnectionFailure.Rejected, HttpStatusCode.ServiceUnavailable);
            clock.TimestampSeconds = 20; await workspace.Controller.TickAsync(TestContext.Current.CancellationToken);
            window.UpdateLayout();
            Assert.False(workspace.Controller.HasData);
            Assert.False(workspace.FindControl<ContentControl>("ConnectedShellHost")!.IsVisible);
            Assert.Contains("automatically", workspace.FindControl<TextBlock>("HomeReadStatus")!.Text);
            Assert.True(workspace.FindControl<Button>("RetryHomeReports")!.IsEnabled);
            NativeCapture.Save(window, $"home-auto-retry-{suffix}.png");
            Assert.True(workspace.FindControl<Button>("ManageHomeConnection")!.Focus());
            clock.TimestampSeconds = 25; await workspace.Controller.TickAsync(TestContext.Current.CancellationToken);
            Assert.True(workspace.FindControl<Button>("ManageHomeConnection")!.IsFocused);

            var response = new TaskCompletionSource<HomeUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
            service.HomeUpdate = cursor => { requested = cursor; return response.Task; };
            clock.TimestampSeconds = 40;
            var pending = workspace.Controller.TickAsync(TestContext.Current.CancellationToken);
            Assert.True(workspace.Controller.IsBusy);
            Assert.True(workspace.FindControl<Button>("ManageHomeConnection")!.IsFocused);
            response.SetResult(new HomeUpdate(revision.Replace('A', 'B'), ConnectedHomeControllerTests.Snapshot()));
            await pending;
            window.UpdateLayout();
            Assert.Null(requested);
            Assert.True(workspace.Controller.HasData);
            Assert.Same(editor, workspace.GetVisualDescendants().OfType<HomeEditorScreen>().Single());
            Assert.Equal("Our unsaved home name", editor.FindControl<TextBox>("HomeNameInput")!.Text);
            NativeCapture.Save(window, $"home-recovered-draft-{suffix}.png");

            service.HomeUpdate = _ => throw new CompanionConnectionException(ConnectionFailure.Rejected, HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(95));
            clock.TimestampSeconds = 60; await workspace.Controller.TickAsync(TestContext.Current.CancellationToken);
            window.UpdateLayout();
            Assert.False(workspace.FindControl<Button>("RetryHomeReports")!.IsEnabled);
            Assert.True(workspace.FindControl<Button>("ManageHomeConnection")!.IsEnabled);
            NativeCapture.Save(window, $"home-server-wait-{suffix}.png");
            Assert.True(workspace.FindControl<Button>("ManageHomeConnection")!.Focus());
            clock.TimestampSeconds = 65; await workspace.Controller.TickAsync(TestContext.Current.CancellationToken);
            Assert.True(workspace.FindControl<Button>("ManageHomeConnection")!.IsFocused);
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    private static void Click(Control control, string name) => control.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}

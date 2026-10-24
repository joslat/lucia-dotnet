using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.Connection;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class ConnectedActivityWorkspaceTests
{
    [AvaloniaFact]
    public async Task ConnectedHomeDisplaysVerifiedActivityButAnActivityOnlyFailureKeepsHomeReports()
    {
        var service = ConnectedHomeControllerTests.Service();
        service.Activity = () => Task.FromResult(ConnectedActivityControllerTests.Snapshot());
        using var workspace = new ConnectedHomeWorkspace(service, Identity(), null);
        var window = new Window { Width = 1000, Height = 900, Content = workspace }; window.Show();
        try
        {
            await workspace.Shell.ProfileSession.InitializeAsync(TestContext.Current.CancellationToken);
            workspace.State.ReplaceHome(ConnectedHomeControllerTests.Setup(), notifyPreferences: false);
            await workspace.RefreshAsync(TestContext.Current.CancellationToken); window.UpdateLayout();
            Assert.True(workspace.Controller.HasData);
            Assert.Single(workspace.GetVisualDescendants().OfType<LuciaStagePath>());
            Assert.Contains("Working", ConnectedActivityScreenTests.Text(workspace));
            service.Activity = () => throw new ActivityAccessException();
            await workspace.ActivityController.RefreshAsync(TestContext.Current.CancellationToken); window.UpdateLayout();
            Assert.True(workspace.Controller.HasData);
            Assert.Equal("Closed", Assert.Single(Assert.Single(workspace.State.Home.Places).Observations).CurrentText);
            Assert.Empty(workspace.GetVisualDescendants().OfType<LuciaStagePath>());
            Assert.Contains("not permitted", ConnectedActivityScreenTests.Text(workspace));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ActivityRevocationHidesProtectedHomeAndRequestsConnectionRecoveryOnce()
    {
        var service = ConnectedHomeControllerTests.Service();
        service.Activity = () => Task.FromResult(ConnectedActivityControllerTests.Snapshot());
        using var workspace = new ConnectedHomeWorkspace(service, Identity(), null);
        var recovery = 0; workspace.ConnectionRequested += () => recovery++;
        var window = new Window { Width = 390, Height = 900, Content = workspace }; window.Show();
        try
        {
            await workspace.Shell.ProfileSession.InitializeAsync(TestContext.Current.CancellationToken);
            workspace.State.ReplaceHome(ConnectedHomeControllerTests.Setup(), notifyPreferences: false);
            await workspace.RefreshAsync(TestContext.Current.CancellationToken);
            service.Activity = () => throw new CompanionConnectionException(ConnectionFailure.Unauthorized);
            await workspace.ActivityController.RefreshAsync(TestContext.Current.CancellationToken); window.UpdateLayout();
            Assert.False(workspace.Controller.HasData);
            Assert.All(workspace.State.Home.Places, place => Assert.Empty(place.Observations));
            Assert.Null(workspace.ActivityController.Snapshot);
            Assert.Equal(1, recovery);
        }
        finally { window.Close(); }
    }

    internal static ConnectionSession Identity()
    {
        var home = TestCompanionConnection.Session();
        return new(home.Endpoint, new(home.Session.SessionId, home.Session.DeviceName, home.Session.ExpiresAt, home.Session.HomeId,
            home.Session.MemberId, home.Session.Places, canReadActivity: true));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActivityLossAndForegroundRecoveryPreserveTheOpenSetupAndItsUnsavedInput(bool denied)
    {
        var service = ConnectedHomeControllerTests.Service();
        service.Activity = () => Task.FromResult(ConnectedActivityControllerTests.Snapshot());
        using var workspace = new ConnectedHomeWorkspace(service, Identity(), null);
        var window = new Window { Width = 390, Height = 900, Content = workspace }; window.Show();
        try
        {
            await workspace.Shell.ProfileSession.InitializeAsync(TestContext.Current.CancellationToken);
            workspace.State.ReplaceHome(ConnectedHomeControllerTests.Setup(), notifyPreferences: false);
            await workspace.RefreshAsync(TestContext.Current.CancellationToken);
            workspace.Shell.FindControl<Button>("BuildHomeButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.UpdateLayout();
            var editor = workspace.GetVisualDescendants().OfType<HomeEditorScreen>().Single();
            var input = editor.FindControl<TextBox>("HomeNameInput")!;
            input.Text = "Our unsaved household name"; input.Focus();
            service.Activity = () => throw (denied ? new ActivityAccessException() : new CompanionConnectionException(ConnectionFailure.Transport));
            await workspace.ActivityController.RefreshAsync(TestContext.Current.CancellationToken);
            window.UpdateLayout();
            Assert.True(workspace.Controller.HasData);
            Assert.Same(editor, workspace.GetVisualDescendants().OfType<HomeEditorScreen>().Single());
            Assert.Equal("Our unsaved household name", input.Text);
            Assert.True(input.IsFocused);
            workspace.Suspend();
            Assert.Null(workspace.ActivityController.Snapshot);
            service.Activity = () => Task.FromResult(ConnectedActivityControllerTests.Snapshot());
            await workspace.ResumeAsync(); window.UpdateLayout();
            Assert.Same(editor, workspace.GetVisualDescendants().OfType<HomeEditorScreen>().Single());
            Assert.Equal("Our unsaved household name", input.Text);
            Assert.NotNull(workspace.ActivityController.Snapshot);
        }
        finally { window.Close(); }
    }
}

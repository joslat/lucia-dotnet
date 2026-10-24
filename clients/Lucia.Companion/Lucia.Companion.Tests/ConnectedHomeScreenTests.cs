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

public sealed class ConnectedHomeScreenTests
{
    [AvaloniaFact]
    public async Task VerifiedConnectionOpensAnEmptyRealHomeThenExplicitImportSuppliesItsReports()
    {
        var service = ConnectedHomeControllerTests.Service();
        var root = new CompanionRoot(service, connectedStoreFactory: _ => null);
        var window = new Window { Width = 390, Height = 900, Content = root };
        window.Show();
        try
        {
            await root.PendingOperation;
            Click(root.GetVisualDescendants().OfType<ConnectionScreen>().Single(), "OpenConnectedHome");
            await root.PendingOperation;
            window.UpdateLayout();
            var workspace = root.GetVisualDescendants().OfType<ConnectedHomeWorkspace>().Single();
            Assert.False(workspace.State.IsDemo);
            Assert.Empty(workspace.State.Home.Places);
            Click(workspace.Shell, "BuildHomeButton");
            window.UpdateLayout();
            var editor = workspace.GetVisualDescendants().OfType<HomeEditorScreen>().Single();
            Click(editor, "ImportPlaces");
            window.UpdateLayout();
            var import = workspace.GetVisualDescendants().OfType<HomeImportScreen>().Single();
            import.GetVisualDescendants().OfType<HomeImportPlaceRow>().Single().FindControl<CheckBox>("SelectImportPlace")!.IsChecked = true;
            Click(import, "PreviewImport"); Click(import, "AddImportToDraft"); Click(editor, "SaveHome");
            window.UpdateLayout();
            Assert.Equal("Closed", Assert.Single(Assert.Single(workspace.State.Home.Places).Observations).CurrentText);
            Assert.DoesNotContain(workspace.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible), text => text.Text?.Contains("Synthetic readings", StringComparison.Ordinal) == true);
            Assert.False(workspace.Shell.FindControl<Button>("StoryButton")!.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task RevocationReturnsToConnectionAndNeverShowsTheOfflineDemo()
    {
        var service = ConnectedHomeControllerTests.Service();
        var root = new CompanionRoot(service, connectedStoreFactory: _ => null);
        var window = new Window { Width = 1000, Height = 900, Content = root };
        window.Show();
        try
        {
            await root.PendingOperation;
            Click(root.GetVisualDescendants().OfType<ConnectionScreen>().Single(), "OpenConnectedHome");
            await root.PendingOperation;
            window.UpdateLayout();
            var workspace = root.GetVisualDescendants().OfType<ConnectedHomeWorkspace>().Single();
            service.Home = () => throw new CompanionConnectionException(ConnectionFailure.Unauthorized);
            service.Restore = () => { service.HasSavedConnection = false; return Task.FromResult<ConnectionSession?>(null); };
            await workspace.RefreshAsync(TestContext.Current.CancellationToken);
            await root.PendingOperation;
            window.UpdateLayout();
            Assert.False(root.State.IsConnected);
            Assert.Single(root.GetVisualDescendants().OfType<ConnectionScreen>());
            Assert.Empty(root.GetVisualDescendants().OfType<PlaceOrb>());
            Assert.False(workspace.Controller.HasData);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ForegroundVerificationPreservesTheSameUnsavedEditorWithoutShowingOldReportsWhileSuspended()
    {
        var service = ConnectedHomeControllerTests.Service();
        var root = new CompanionRoot(service, connectedStoreFactory: _ => null);
        var window = new Window { Width = 1000, Height = 900, Content = root };
        window.Show();
        try
        {
            await root.PendingOperation;
            Click(root.GetVisualDescendants().OfType<ConnectionScreen>().Single(), "OpenConnectedHome");
            await root.PendingOperation;
            window.UpdateLayout();
            var workspace = root.GetVisualDescendants().OfType<ConnectedHomeWorkspace>().Single();
            Click(workspace.Shell, "BuildHomeButton");
            window.UpdateLayout();
            var editor = workspace.GetVisualDescendants().OfType<HomeEditorScreen>().Single();
            editor.FindControl<TextBox>("HomeNameInput")!.Text = "Night house";
            root.SuspendConnectionPolling();
            Assert.False(workspace.Controller.HasData);
            await root.VerifyForegroundAsync();
            window.UpdateLayout();
            Assert.Same(editor, workspace.GetVisualDescendants().OfType<HomeEditorScreen>().Single());
            Assert.Equal("Night house", editor.FindControl<TextBox>("HomeNameInput")!.Text);
            Assert.True(workspace.Controller.HasData);
        }
        finally { window.Close(); }
    }

    private static void Click(Control parent, string name) => parent.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}

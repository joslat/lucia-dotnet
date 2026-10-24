using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.Screens;
using Lucia.Companion.Connection;
using Lucia.Companion.Desktop.Security;
using Xunit;

namespace Lucia.Companion.Tests;

/// <summary>Opt-in full native UI/HTTPS/SQLite/HA-wire proof. Only the dedicated synthetic loopback fixture is accepted.</summary>
public sealed class ConnectedHomeWireScreenTests
{
    public static bool MissingFixture => !OperatingSystem.IsWindows()
        || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LUCIA_COMPANION_WIRE_URL"));

    [AvaloniaFact(Skip = "Requires Windows DPAPI and the isolated --native-home fixture.", SkipWhen = nameof(MissingFixture))]
    public async Task NativeImportSaveReopenRefreshAndRevocationCrossTheActualProtectedTransport()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("Windows DPAPI qualification requires Windows."); return; }
        var endpoint = new Uri(Environment.GetEnvironmentVariable("LUCIA_COMPANION_WIRE_URL")!);
        var fingerprint = Environment.GetEnvironmentVariable("LUCIA_COMPANION_WIRE_CERT")!;
        Assert.Equal("https", endpoint.Scheme);
        Assert.True(endpoint.IsLoopback);
        Assert.Matches("^[A-F0-9]{64}$", fingerprint);
        var directory = Directory.CreateTempSubdirectory("lucia-native-ui-wire-");
        HttpClientHandler Handler() => new()
        {
            AllowAutoRedirect = false, UseCookies = false,
            ServerCertificateCustomValidationCallback = (_, presented, _, _) => presented?.GetCertHashString(HashAlgorithmName.SHA256) == fingerprint
        };
        using var owner = new HttpClient(Handler()) { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(20) };
        owner.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        using var service = new CompanionConnection(new ProtectedSessionVault(directory.FullName, new WindowsDeviceSecretProtector()),
            address => { Assert.Equal(endpoint, address); return new(address, Handler()); });
        var profilePath = Path.Combine(directory.FullName, "metadata.json");
        var root = new CompanionRoot(service, connectedStoreFactory: _ => new LocalHomeStore(profilePath));
        var window = new Window { Width = 390, Height = 900, Content = root };
        window.Show();
        try
        {
            await root.PendingOperation;
            root.State.HostAddress = endpoint.AbsoluteUri;
            root.State.DeviceName = "Synthetic native wire phone";
            await root.State.StartAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(root.State.Prompt);
            using var review = await owner.PostAsJsonAsync("/api/companion/owner/pairings/review", new { userCode = root.State.Prompt.UserCode }, TestContext.Current.CancellationToken);
            review.EnsureSuccessStatusCode();
            using var body = JsonDocument.Parse(await review.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var pairingId = body.RootElement.GetProperty("id").GetString();
            using var approve = await owner.PostAsJsonAsync($"/api/companion/owner/pairings/{pairingId}/approve", new
            {
                codeConfirmed = true,
                grant = new { homeId = "home-a", memberId = "member-a", places = new[] { new { placeId = "porch", fields = new[] { "Temperature" } } } }
            }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);
            // Respect the server's poll interval, then exercise the same tick used by the screen.
            await Task.Delay(TimeSpan.FromSeconds(root.State.Prompt.PollIntervalSeconds), TestContext.Current.CancellationToken);
            await root.State.TickAsync(TestContext.Current.CancellationToken);
            Assert.True(root.State.IsConnected);
            var sessionId = root.State.Connection!.Session.SessionId;
            Click(root.GetVisualDescendants().OfType<ConnectionScreen>().Single(), "OpenConnectedHome");
            await root.PendingOperation;
            window.UpdateLayout();
            var workspace = root.GetVisualDescendants().OfType<ConnectedHomeWorkspace>().Single();
            Assert.True(workspace.Controller.HasData, workspace.Controller.StatusText);
            Assert.Empty(workspace.State.Home.Places);
            Click(workspace.Shell, "BuildHomeButton"); window.UpdateLayout();
            var editor = workspace.GetVisualDescendants().OfType<HomeEditorScreen>().Single();
            Click(editor, "ImportPlaces"); window.UpdateLayout();
            var import = workspace.GetVisualDescendants().OfType<HomeImportScreen>().Single();
            var room = Assert.Single(import.GetVisualDescendants().OfType<HomeImportPlaceRow>());
            room.FindControl<CheckBox>("SelectImportPlace")!.IsChecked = true;
            Click(import, "PreviewImport"); Click(import, "AddImportToDraft"); Click(editor, "SaveHome");
            await workspace.Shell.ProfileSession.PendingSave;
            var observation = Assert.Single(Assert.Single(workspace.State.Home.Places).Observations);
            Assert.Equal("21.5 °C", observation.CurrentText);
            Assert.DoesNotContain("private-wire", await File.ReadAllTextAsync(profilePath, TestContext.Current.CancellationToken));
            var bytes = await File.ReadAllBytesAsync(profilePath, TestContext.Current.CancellationToken);
            await workspace.RefreshAsync(TestContext.Current.CancellationToken);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(profilePath, TestContext.Current.CancellationToken));
            NativeCapture.Save(window, "connected-wire-home-phone.png");
            window.Close();

            var reopenedRoot = new CompanionRoot(service, connectedStoreFactory: _ => new LocalHomeStore(profilePath));
            window = new Window { Width = 1000, Height = 900, Content = reopenedRoot };
            window.Show(); await reopenedRoot.PendingOperation;
            Assert.True(reopenedRoot.State.IsConnected);
            Click(reopenedRoot.GetVisualDescendants().OfType<ConnectionScreen>().Single(), "OpenConnectedHome");
            await reopenedRoot.PendingOperation; window.UpdateLayout();
            var restored = reopenedRoot.GetVisualDescendants().OfType<ConnectedHomeWorkspace>().Single();
            Assert.Equal("21.5 °C", Assert.Single(Assert.Single(restored.State.Home.Places).Observations).CurrentText);
            NativeCapture.Save(window, "connected-wire-home-tablet.png");
            using var revoked = await owner.DeleteAsync($"/api/companion/owner/sessions/{sessionId}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
            await restored.RefreshAsync(TestContext.Current.CancellationToken);
            await reopenedRoot.PendingOperation; window.UpdateLayout();
            Assert.False(reopenedRoot.State.IsConnected);
            Assert.False(service.HasSavedConnection);
            Assert.Empty(reopenedRoot.GetVisualDescendants().OfType<PlaceOrb>());
            Assert.Single(reopenedRoot.GetVisualDescendants().OfType<ConnectionScreen>());
            Assert.Empty(Assert.Single(restored.State.Home.Places).Observations);
        }
        finally { window.Close(); directory.Delete(recursive: true); }
    }

    private static void Click(Control parent, string name) => parent.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}

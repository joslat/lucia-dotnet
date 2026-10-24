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

/// <summary>Production conversation-to-native UI with isolated HTTPS/SQLite/HA-wire and a nonphysical synthetic executor.</summary>
public sealed class ConnectedActivityWireScreenTests
{
    public static bool MissingFixture => ConnectedHomeWireScreenTests.MissingFixture;
    [AvaloniaFact(Skip = "Requires Windows DPAPI and the isolated --native-home fixture.", SkipWhen = nameof(MissingFixture))]
    public async Task ARealAttributedConversationUpdatesTheNativeStageAndRevocationHidesAllProtectedViews()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("Windows DPAPI qualification requires Windows."); return; }
        var endpoint = new Uri(Environment.GetEnvironmentVariable("LUCIA_COMPANION_WIRE_URL")!);
        var fingerprint = Environment.GetEnvironmentVariable("LUCIA_COMPANION_WIRE_CERT")!;
        Assert.Equal("https", endpoint.Scheme); Assert.True(endpoint.IsLoopback); Assert.Matches("^[A-F0-9]{64}$", fingerprint);
        var directory = Directory.CreateTempSubdirectory("lucia-native-activity-wire-");
        HttpClientHandler Handler() => new() { AllowAutoRedirect = false, UseCookies = false,
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) => certificate?.GetCertHashString(HashAlgorithmName.SHA256) == fingerprint };
        using var owner = new HttpClient(Handler()) { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(20) };
        owner.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        using var conversation = new HttpClient(Handler()) { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(30) };
        conversation.DefaultRequestHeaders.Add("Cookie", ".lucia.session=synthetic-cookie");
        using var service = new CompanionConnection(new ProtectedSessionVault(directory.FullName, new WindowsDeviceSecretProtector()), address => new(address, Handler()));
        var window = new Window { Width = 390, Height = 900 };
        Task<HttpResponseMessage>? request = null;
        try
        {
            var prompt = await service.BeginAsync(endpoint, "Synthetic activity phone", TestContext.Current.CancellationToken);
            using var review = await owner.PostAsJsonAsync("/api/companion/owner/pairings/review", new { userCode = prompt.UserCode }, TestContext.Current.CancellationToken);
            review.EnsureSuccessStatusCode();
            var pairing = await review.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            using var approve = await owner.PostAsJsonAsync($"/api/companion/owner/pairings/{pairing.GetProperty("id").GetString()}/approve", new
            { codeConfirmed = true, grant = new { homeId = "home-a", memberId = "member-a", canReadActivity = true,
                places = new[] { new { placeId = "porch", fields = new[] { "Temperature" } } } } }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);
            await Task.Delay(TimeSpan.FromSeconds(prompt.PollIntervalSeconds), TestContext.Current.CancellationToken);
            Assert.Equal(PairingPollStatus.Success, (await service.PollAsync(TestContext.Current.CancellationToken)).Status);
            var root = new CompanionRoot(service, connectedStoreFactory: _ => new LocalHomeStore(Path.Combine(directory.FullName, "metadata.json")));
            window.Content = root; window.Show(); await root.PendingOperation;
            root.GetVisualDescendants().OfType<ConnectionScreen>().Single().FindControl<Button>("OpenConnectedHome")!
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await root.PendingOperation; window.UpdateLayout();
            var workspace = root.GetVisualDescendants().OfType<ConnectedHomeWorkspace>().Single();
            Assert.True(workspace.Controller.HasData);
            using var reset = await owner.PostAsync("/synthetic/conversation/reset", null, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
            request = conversation.PostAsJsonAsync("/api/conversation/", new
                { text = "PRIVATE_NATIVE_REQUEST", context = new { conversationId = "PRIVATE_NATIVE_CONVERSATION_" + Guid.NewGuid().ToString("N") } }, TestContext.Current.CancellationToken);
            var entered = false;
            for (var attempt = 0; attempt < 100 && !entered; attempt++)
            {
                using var status = await owner.GetAsync("/synthetic/conversation/status", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, status.StatusCode);
                entered = (await status.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("waiting").GetBoolean();
                if (!entered) { await Task.Delay(20, TestContext.Current.CancellationToken); }
            }
            Assert.True(entered, "The synthetic router must hold the actual conversation request before reading its real Working phase.");
            await workspace.ActivityController.RefreshAsync(TestContext.Current.CancellationToken); window.UpdateLayout();
            var working = Assert.Single(workspace.ActivityController.Snapshot!.Requests, item => item.State == RequestActivityState.Working);
            Assert.Equal(RequestActivityState.Working, working.State);
            Assert.Equal(RequestActivityCategory.Lighting, working.Category);
            Assert.Equal("Porch", Assert.Single(working.CurrentTargetAreas!.Places).Name);
            Assert.NotEmpty(workspace.GetVisualDescendants().OfType<LuciaStagePath>());
            Assert.Contains("Working", ConnectedActivityScreenTests.Text(workspace));
            Assert.Contains("Lighting request", ConnectedActivityScreenTests.Text(workspace));
            Assert.Contains("Current target areas: Porch", ConnectedActivityScreenTests.Text(workspace));
            Assert.DoesNotContain("PRIVATE_", ConnectedActivityScreenTests.Text(workspace));
            NativeCapture.Save(window, "activity-wire-working-phone.png");
            using var release = await owner.PostAsync("/synthetic/conversation/release", null, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, release.StatusCode);
            using var response = await request; Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await workspace.ActivityController.RefreshAsync(TestContext.Current.CancellationToken); window.UpdateLayout();
            Assert.Equal(RequestActivityState.ResponseReady, workspace.ActivityController.Snapshot!.Requests.Single(item => item.RequestId == working.RequestId).State);
            Assert.Contains("Response ready", ConnectedActivityScreenTests.Text(workspace));
            window.Width = 1000; window.UpdateLayout();
            NativeCapture.Save(window, "activity-wire-response-tablet.png");
            using var revoke = await owner.DeleteAsync($"/api/companion/owner/sessions/{workspace.Identity.Session.SessionId}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
            await workspace.ActivityController.RefreshAsync(TestContext.Current.CancellationToken);
            await root.PendingOperation; window.UpdateLayout();
            Assert.False(service.HasSavedConnection); Assert.False(root.State.IsConnected);
            Assert.Empty(root.GetVisualDescendants().OfType<LuciaStagePath>());
            Assert.Null(workspace.ActivityController.Snapshot); Assert.False(workspace.Controller.HasData);
        }
        finally
        {
            using var release = await owner.PostAsync("/synthetic/conversation/release", null, TestContext.Current.CancellationToken);
            if (request is not null) { using var response = await request; }
            window.Close(); directory.Delete(recursive: true);
        }
    }
}

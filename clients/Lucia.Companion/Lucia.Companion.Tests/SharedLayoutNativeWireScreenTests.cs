using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.Screens;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;
using Lucia.Companion.Desktop.Security;
using Xunit;

namespace Lucia.Companion.Tests;

/// <summary>Two actual native UI clients, protected sessions, HTTPS/SQLite and synthetic HA protocol. Not an installed-device claim.</summary>
public sealed class SharedLayoutNativeWireScreenTests
{
    public static bool MissingFixture => !OperatingSystem.IsWindows() || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LUCIA_COMPANION_WIRE_URL"));

    [AvaloniaFact(Skip = "Requires Windows DPAPI and the isolated --native-home fixture.", SkipWhen = nameof(MissingFixture))]
    public async Task IndependentNativeClientsReviewOwnerUpdatesRefuseStaleAdoptionSaveAndIsolateRevocation()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("Windows DPAPI requires Windows."); return; }
        var endpoint = new Uri(Environment.GetEnvironmentVariable("LUCIA_COMPANION_WIRE_URL")!);
        var fingerprint = Environment.GetEnvironmentVariable("LUCIA_COMPANION_WIRE_CERT")!;
        Assert.Equal("https", endpoint.Scheme); Assert.True(endpoint.IsLoopback); Assert.Matches("^[A-F0-9]{64}$", fingerprint);
        HttpClientHandler Handler() => new() { AllowAutoRedirect = false, UseCookies = false,
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) => certificate?.GetCertHashString(HashAlgorithmName.SHA256) == fingerprint };
        var directory = Directory.CreateTempSubdirectory("lucia-shared-native-wire-");
        using var owner = new HttpClient(Handler()) { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(20) };
        owner.DefaultRequestHeaders.Add("X-API-Key", "synthetic-owner");
        using var phone = new CompanionConnection(new ProtectedSessionVault(Path.Combine(directory.FullName, "phone"), new WindowsDeviceSecretProtector()), address => new(address, Handler()));
        using var tablet = new CompanionConnection(new ProtectedSessionVault(Path.Combine(directory.FullName, "tablet"), new WindowsDeviceSecretProtector()), address => new(address, Handler()));
        Window? phoneWindow = null; Window? tabletWindow = null;
        ConnectedHomeWorkspace? phoneHome = null; ConnectedHomeWorkspace? tabletHome = null;
        try
        {
            using var context = await owner.GetAsync("/api/companion/owner/layout", TestContext.Current.CancellationToken);
            Assert.True(context.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.OK);
            var ownerContext = await context.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            var source = ownerContext.GetProperty("sourceBaseUrl").GetString()!;
            var revision = await SaveOwnerAsync(owner, source, "Front step", context.StatusCode == HttpStatusCode.OK ? ownerContext.GetProperty("revision").GetString() : null);
            await PairAsync(phone, owner, endpoint, "Native shared phone");
            await PairAsync(tablet, owner, endpoint, "Native shared tablet");
            var phonePath = Path.Combine(directory.FullName, "phone-metadata.json");
            var tabletPath = Path.Combine(directory.FullName, "tablet-metadata.json");
            var phoneRoot = new CompanionRoot(phone, connectedStoreFactory: _ => new LocalHomeStore(phonePath));
            var tabletRoot = new CompanionRoot(tablet, connectedStoreFactory: _ => new LocalHomeStore(tabletPath));
            phoneWindow = new Window { Width = 390, Height = 900, Content = phoneRoot }; phoneWindow.Show(); await phoneRoot.PendingOperation;
            tabletWindow = new Window { Width = 1194, Height = 900, Content = tabletRoot }; tabletWindow.Show(); await tabletRoot.PendingOperation;
            HomeSharedLayoutScreenTests.Click(phoneRoot.GetVisualDescendants().OfType<ConnectionScreen>().Single(), "OpenConnectedHome"); await phoneRoot.PendingOperation;
            HomeSharedLayoutScreenTests.Click(tabletRoot.GetVisualDescendants().OfType<ConnectionScreen>().Single(), "OpenConnectedHome"); await tabletRoot.PendingOperation;
            phoneHome = phoneRoot.GetVisualDescendants().OfType<ConnectedHomeWorkspace>().Single();
            tabletHome = tabletRoot.GetVisualDescendants().OfType<ConnectedHomeWorkspace>().Single();
            Assert.True(phoneHome.Controller.HasData); Assert.True(tabletHome.Controller.HasData);
            var phoneEditor = OpenEditor(phoneHome); phoneEditor.FindControl<TextBox>("HomeNameInput")!.Text = "Keep my phone draft";
            var phoneReview = await OpenReviewAsync(phoneEditor);
            var tabletEditor = OpenEditor(tabletHome); var tabletReview = await OpenReviewAsync(tabletEditor);
            Assert.Equal("Front step", Assert.Single(phoneReview.Controller.Plan!.Preview.Places).Name);
            Assert.DoesNotContain(phoneReview.GetVisualDescendants().OfType<TextBlock>(), item => item.Text?.Contains("PRIVATE", StringComparison.Ordinal) == true);
            revision = await SaveOwnerAsync(owner, source, "Front porch", revision);
            HomeSharedLayoutScreenTests.Click(phoneReview, "AddSharedSetupToDraft"); await phoneReview.Controller.PendingOperation;
            HomeSharedLayoutScreenTests.Click(tabletReview, "AddSharedSetupToDraft"); await tabletReview.Controller.PendingOperation;
            Assert.Null(phoneReview.Controller.Plan); Assert.Null(tabletReview.Controller.Plan);
            Assert.Equal("Keep my phone draft", phoneEditor.FindControl<TextBox>("HomeNameInput")!.Text);
            Assert.Empty(phoneHome.State.Home.Places); Assert.False(File.Exists(phonePath));
            await AdoptAndSaveAsync(phoneHome, phoneEditor, phoneReview);
            await AdoptAndSaveAsync(tabletHome, tabletEditor, tabletReview);
            Assert.Equal("Front porch", Assert.Single(phoneHome.State.Home.Places).Name);
            Assert.Equal("Front porch", Assert.Single(tabletHome.State.Home.Places).Name);
            Assert.Equal("21.5 °C", Assert.Single(phoneHome.State.Home.Places[0].Observations).CurrentText);
            Assert.DoesNotContain("21.5", await File.ReadAllTextAsync(phonePath, TestContext.Current.CancellationToken));
            phoneHome.State.SelectPlace("front"); phoneHome.State.TogglePin();
            phoneHome.State.SetSectionOrder([HomeSection.LuciaNow, HomeSection.Places, HomeSection.Attention]);
            await phoneHome.Shell.ProfileSession.PendingSave;
            var phoneProfile = (await new LocalHomeStore(phonePath).LoadAsync(TestContext.Current.CancellationToken))!;
            Assert.Equal(new[] { "front" }, phoneProfile.PinnedPlaceIds);
            Assert.Equal(new[] { HomeSection.LuciaNow, HomeSection.Places, HomeSection.Attention }, phoneProfile.SectionOrder);
            var newPhoneEditor = OpenEditor(phoneHome); var newPhoneReview = await OpenReviewAsync(newPhoneEditor);
            using var revoked = await owner.DeleteAsync($"/api/companion/owner/sessions/{phoneHome.Identity.Session.SessionId}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
            HomeSharedLayoutScreenTests.Click(newPhoneReview, "AddSharedSetupToDraft"); await newPhoneReview.Controller.PendingOperation;
            Assert.Null(newPhoneReview.Controller.Plan); Assert.True(newPhoneReview.Controller.RequiresConnectionReview);
            Assert.False(phone.HasSavedConnection); Assert.True(tablet.HasSavedConnection);
            HomeSharedLayoutScreenTests.Click(newPhoneReview, "SharedConnection"); await phoneRoot.PendingOperation;
            Assert.False(phoneRoot.State.IsConnected); Assert.Single(phoneRoot.GetVisualDescendants().OfType<ConnectionScreen>());
            Assert.Equal("Front porch", Assert.Single((await tablet.ReadSharedLayoutAsync(tabletHome.Identity, TestContext.Current.CancellationToken))!.Places).Name);
            Assert.Equal("Front porch", Assert.Single(tabletHome.State.Home.Places).Name);
        }
        finally { phoneWindow?.Close(); tabletWindow?.Close(); phoneHome?.Dispose(); tabletHome?.Dispose(); directory.Delete(recursive: true); }
    }

    private static HomeEditorScreen OpenEditor(ConnectedHomeWorkspace workspace)
    {
        HomeSharedLayoutScreenTests.Click(workspace.Shell, "BuildHomeButton");
        (TopLevel.GetTopLevel(workspace) as Window)?.UpdateLayout();
        return workspace.GetVisualDescendants().OfType<HomeEditorScreen>().Single();
    }
    private static async Task<HomeSharedLayoutScreen> OpenReviewAsync(HomeEditorScreen editor)
    {
        HomeSharedLayoutScreenTests.Click(editor, "ReviewSharedSetup");
        (TopLevel.GetTopLevel(editor) as Window)?.UpdateLayout();
        var review = editor.GetVisualDescendants().OfType<HomeSharedLayoutScreen>().Single(); await review.Controller.PendingOperation;
        Assert.NotNull(review.Controller.Plan); return review;
    }
    private static async Task AdoptAndSaveAsync(ConnectedHomeWorkspace workspace, HomeEditorScreen editor, HomeSharedLayoutScreen review)
    {
        HomeSharedLayoutScreenTests.Click(review, "ReadSharedSetup"); await review.Controller.PendingOperation;
        HomeSharedLayoutScreenTests.Click(review, "AddSharedSetupToDraft"); await review.Controller.PendingOperation;
        Assert.Empty(workspace.State.Home.Places);
        HomeSharedLayoutScreenTests.Click(editor, "SaveHome"); await workspace.Shell.ProfileSession.PendingSave;
    }
    private static async Task PairAsync(CompanionConnection service, HttpClient owner, Uri endpoint, string name)
    {
        var prompt = await service.BeginAsync(endpoint, name, TestContext.Current.CancellationToken);
        using var review = await owner.PostAsJsonAsync("/api/companion/owner/pairings/review", new { userCode = prompt.UserCode }, TestContext.Current.CancellationToken);
        review.EnsureSuccessStatusCode();
        var id = (await review.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetString();
        using var approved = await owner.PostAsJsonAsync($"/api/companion/owner/pairings/{id}/approve", new
        { codeConfirmed = true, grant = new { homeId = "home-a", memberId = "member-a", places = new[] { new { placeId = "porch", fields = new[] { "Temperature" } } } } }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, approved.StatusCode);
        await Task.Delay(TimeSpan.FromSeconds(prompt.PollIntervalSeconds), TestContext.Current.CancellationToken);
        Assert.Equal(PairingPollStatus.Success, (await service.PollAsync(TestContext.Current.CancellationToken)).Status);
    }
    private static async Task<string> SaveOwnerAsync(HttpClient owner, string source, string visibleName, string? revision)
    {
        using var saved = await owner.PutAsJsonAsync("/api/companion/owner/layout", new { expectedRevision = revision, layout = new
        {
            homeId = "home-a", sourceBaseUrl = source, name = "Shared Home",
            places = new[] { new { id = "front", name = visibleName, icon = "Door", sourcePlaceId = "porch" }, new { id = "sleep", name = "PRIVATE_BEDROOM", icon = "Bed", sourcePlaceId = "bedroom" } },
            connections = new[] { new { fromPlaceId = "front", toPlaceId = "sleep" } },
            bindings = new[] { new { placeId = "front", sourcePlaceId = "porch", sourceId = "sensor.porch_temperature", field = "Temperature" }, new { placeId = "sleep", sourcePlaceId = "bedroom", sourceId = "sensor.bedroom_temperature", field = "Temperature" } }
        } }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        return (await saved.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("revision").GetString()!;
    }
}

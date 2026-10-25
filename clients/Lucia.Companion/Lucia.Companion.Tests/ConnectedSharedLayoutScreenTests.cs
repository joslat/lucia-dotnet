using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.Screens;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class ConnectedSharedLayoutScreenTests
{
    [AvaloniaFact]
    public async Task ConnectedAdoptionPreservesMatchingPinsAndSectionOrderAndPersistsOnlyMetadata()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-shared-ui-");
        var store = new LocalHomeStore(Path.Combine(directory.FullName, "metadata.json"));
        var identity = SharedLayoutAdoptionControllerTests.Identity();
        var old = new HomeTopology("home-a", "Local home", [new("shared-porch", "Local porch", PlaceState.Unavailable, "Unknown", [])], []);
        var order = new[] { HomeSection.LuciaNow, HomeSection.Places, HomeSection.Attention };
        await store.SaveAsync(HomeProfile.From(old, ["shared-porch"], order), null, TestContext.Current.CancellationToken);
        var service = SharedLayoutAdoptionControllerTests.Service((expected, _) => { Assert.Same(identity, expected); return Task.FromResult<SharedHomeLayout?>(SharedLayoutAdoptionControllerTests.Layout()); });
        service.Home = () => Task.FromResult(Snapshot());
        using var workspace = new ConnectedHomeWorkspace(service, identity, store);
        var window = new Window { Width = 1194, Height = 900, Content = workspace }; window.Show();
        try
        {
            await workspace.ResumeAsync();
            Assert.True(workspace.State.IsPinned("shared-porch"));
            HomeSharedLayoutScreenTests.Click(workspace.Shell, "BuildHomeButton"); window.UpdateLayout();
            var editor = workspace.GetVisualDescendants().OfType<HomeEditorScreen>().Single();
            HomeSharedLayoutScreenTests.Click(editor, "ReviewSharedSetup"); window.UpdateLayout();
            var review = workspace.GetVisualDescendants().OfType<HomeSharedLayoutScreen>().Single();
            await review.Controller.PendingOperation;
            HomeSharedLayoutScreenTests.Click(review, "AddSharedSetupToDraft"); await review.Controller.PendingOperation;
            Assert.Equal("Local home", workspace.State.Home.Name);
            HomeSharedLayoutScreenTests.Click(editor, "SaveHome"); await workspace.Shell.ProfileSession.PendingSave;
            Assert.Equal("Owner home", workspace.State.Home.Name); Assert.True(workspace.State.IsPinned("shared-porch"));
            Assert.Equal(order, workspace.State.SectionOrder);
            Assert.Equal("21.5 °C", Assert.Single(Assert.Single(workspace.State.Home.Places).Observations).CurrentText);
            var restored = (await store.LoadAsync(TestContext.Current.CancellationToken))!;
            Assert.Equal("Owner home", restored.HomeName); Assert.Equal(new[] { "shared-porch" }, restored.PinnedPlaceIds);
            Assert.Equal(order, restored.SectionOrder);
            Assert.DoesNotContain("21.5", await File.ReadAllTextAsync(Path.Combine(directory.FullName, "metadata.json"), TestContext.Current.CancellationToken));
        }
        finally { window.Close(); directory.Delete(recursive: true); }
    }

    [AvaloniaFact]
    public async Task SuspendAndResumePreserveTheEditorButRequireFreshSharedReview()
    {
        var service = SharedLayoutAdoptionControllerTests.Service((_, _) => Task.FromResult<SharedHomeLayout?>(SharedLayoutAdoptionControllerTests.Layout()));
        service.Home = () => Task.FromResult(Snapshot());
        using var workspace = new ConnectedHomeWorkspace(service, SharedLayoutAdoptionControllerTests.Identity(), null);
        var window = new Window { Width = 390, Height = 900, Content = workspace }; window.Show();
        try
        {
            await workspace.ResumeAsync(); HomeSharedLayoutScreenTests.Click(workspace.Shell, "BuildHomeButton"); window.UpdateLayout();
            var editor = workspace.GetVisualDescendants().OfType<HomeEditorScreen>().Single();
            editor.FindControl<TextBox>("HomeNameInput")!.Text = "Keep my pending edit";
            HomeSharedLayoutScreenTests.Click(editor, "ReviewSharedSetup"); window.UpdateLayout();
            var review = workspace.GetVisualDescendants().OfType<HomeSharedLayoutScreen>().Single();
            await review.Controller.PendingOperation; Assert.NotNull(review.Controller.Plan);
            workspace.Suspend(); Assert.Null(review.Controller.Plan);
            await workspace.ResumeAsync(); window.UpdateLayout();
            Assert.Same(editor, workspace.GetVisualDescendants().OfType<HomeEditorScreen>().Single());
            Assert.Null(review.Controller.Plan); Assert.False(review.FindControl<Button>("AddSharedSetupToDraft")!.IsEnabled);
            editor.RequestBack(); Assert.Equal("Keep my pending edit", editor.FindControl<TextBox>("HomeNameInput")!.Text);
        }
        finally { window.Close(); }
    }

    internal static HomeSnapshot Snapshot() => new("home-a", DateTimeOffset.UtcNow, 1800,
        [new("porch", "Porch", [new("sensor.porch_temperature", "Porch temperature", ObservationRole.Temperature)],
            [new("sensor.porch_temperature", ObservationRole.Temperature, ObservationQuality.Current, DateTimeOffset.UtcNow.AddSeconds(-1), 21.5, null, "°C")])]);
}

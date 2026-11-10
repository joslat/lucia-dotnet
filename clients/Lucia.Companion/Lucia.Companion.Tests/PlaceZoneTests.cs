using Lucia.Companion.App;
using Lucia.Companion.App.Configuration;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class PlaceZoneTests
{
    [Theory]
    [InlineData(null, "No zone")]
    [InlineData(PlaceZone.Inside, "Inside")]
    [InlineData(PlaceZone.Outside, "Outside")]
    [InlineData(PlaceZone.OwnBuilding, "Own building")]
    public void ZonesHaveReadableLabels(PlaceZone? zone, string label)
    {
        Assert.Equal(label, PlaceZones.Label(zone));
        Assert.Equal(zone, PlaceZones.FromChoice(PlaceZones.Choices.ToList().IndexOf(label)));
        Assert.Equal(PlaceZones.Choices.ToList().IndexOf(label), PlaceZones.ChoiceIndex(zone));
    }

    [Fact]
    public void TheEditorChangesAZoneAndCountsItAsAChange()
    {
        var editor = new HomeEditorViewModel(SampleHome.Create());
        var garden = editor.Places.Single(place => place.Id == "garden");
        editor.UpdatePlace("garden", garden.Name, garden.Icon, PlaceZone.Inside);
        Assert.True(editor.HasChanges);
        Assert.Equal(PlaceZone.Inside, editor.Places.Single(place => place.Id == "garden").Zone);
        editor.UpdatePlace("garden", garden.Name, garden.Icon);
        Assert.Null(editor.Places.Single(place => place.Id == "garden").Zone);
    }

    [Fact]
    public async Task ZonesAreSavedLocallyAndOlderFilesLoadWithoutThem()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"lucia-companion-zone-{Guid.NewGuid():N}");
        var file = Path.Combine(folder, "home.json");
        try
        {
            var store = new LocalHomeStore(file);
            var home = SampleHome.Create();
            await store.SaveAsync(HomeProfile.From(home, []), null, TestContext.Current.CancellationToken);
            var json = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
            Assert.Contains("\"OwnBuilding\"", json, StringComparison.Ordinal);
            var restored = (await store.LoadAsync(TestContext.Current.CancellationToken))!.ApplyTo(home);
            Assert.Equal(home.Places.Select(place => place.Zone), restored.Places.Select(place => place.Zone));

            var older = new HomeProfile(1, home.Id, home.Name, home.Places.Select(place => new PlaceSetting(place.Id, place.Name, place.Icon)).ToArray(), [], []);
            var unzoned = older.ApplyTo(home);
            Assert.All(unzoned.Places, place => Assert.Null(place.Zone));
        }
        finally { if (Directory.Exists(folder)) { Directory.Delete(folder, recursive: true); } }
    }

    [Fact]
    public void SharedAdoptionKeepsLocalZonesForMatchingPlaces()
    {
        var layout = SharedLayoutAdoptionPlanTests.Layout();
        var porchId = layout.Places[0].Id;
        var original = new HomeTopology(layout.HomeId, "Personal home",
            [new(porchId, "Local porch", PlaceState.Quiet, "Prior", []) { Zone = PlaceZone.Outside }], []);
        var plan = SharedLayoutAdoptionPlan.Create(original, layout, SharedLayoutAdoptionPlanTests.Identity());
        Assert.Equal(PlaceZone.Outside, plan.Preview.Places.Single(place => place.Id == porchId).Zone);
        Assert.All(plan.Preview.Places.Where(place => place.Id != porchId), place => Assert.Null(place.Zone));
    }

    [Fact]
    public void UndefinedZonesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new HomeTopology("h", "Home", [new("a", "A", PlaceState.Quiet, "", []) { Zone = (PlaceZone)42 }], []));
    }
}

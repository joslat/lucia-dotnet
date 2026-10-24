using Lucia.Companion.App;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class ObservationBindingTests
{
    [Fact]
    public void BindingsAreValidatedMetadataNotConnectionsOrSensorReadings()
    {
        var home = SampleHome.Create();
        ObservationBinding[] bindings = [new("garden", "binary_sensor.garden_gate", ObservationRole.Contact)];
        var configured = new HomeTopology(home.Id, home.Name, home.Places, home.Connections, bindings);
        bindings[0] = new("living", "sensor.indoor", ObservationRole.Temperature);
        Assert.Equal("garden", Assert.Single(configured.Bindings).PlaceId);
        Assert.Equal(home.Connections, configured.Connections);
        Assert.Equal(home.Places[1].Observations, configured.Places[1].Observations);
        Assert.Throws<ArgumentException>(() => new HomeTopology(home.Id, home.Name, home.Places, [],
            [new("outside-home", "sensor.temperature", ObservationRole.Temperature)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HomeTopology(home.Id, home.Name, home.Places, [],
            [new("garden", "sensor.temperature", (ObservationRole)99)]));
        Assert.Throws<ArgumentException>(() => new HomeTopology(home.Id, home.Name, home.Places, [],
            [new("garden", "sensor.temperature", ObservationRole.Temperature), new("living", "sensor.temperature", ObservationRole.Temperature)]));
        Assert.Throws<ArgumentException>(() => new ObservationSource("sensor.invalid", "Invalid", []));
    }

    [Fact]
    public void OnlyCatalogSupportedRolesCanBeBoundAndEditsAreAtomic()
    {
        var original = SampleHome.Create();
        var editor = new HomeEditorViewModel(original);
        var source = new ObservationSource("binary_sensor.garden_gate", "Garden gate", [ObservationRole.Contact]);
        editor.BindSource("garden", source, ObservationRole.Contact);
        Assert.True(editor.HasChanges);
        Assert.Empty(original.Bindings);
        Assert.Throws<ArgumentException>(() => editor.BindSource("garden", source, ObservationRole.Temperature));
        Assert.Throws<ArgumentException>(() => editor.BindSource("living", source, ObservationRole.Contact));
        Assert.Single(editor.Bindings);
        editor.UpdatePlace("garden", "Back garden", PlaceIcon.Garden);
        editor.MovePlace("garden", -1);
        editor.SetHomeName("Our home");
        Assert.Single(editor.Build().Bindings);
        editor.RemovePlace("garden");
        Assert.Empty(editor.Build().Bindings);
        editor.BindSource("living", source, ObservationRole.Contact);
        editor.UnbindSource("living", source.Id, ObservationRole.Contact);
        Assert.Empty(editor.Build().Bindings);
    }

    [Fact]
    public async Task ProfileRoundTripsBindingsWithoutPersistingCatalogOrValues()
    {
        var file = Path.Combine(Path.GetTempPath(), $"lucia-bindings-{Guid.NewGuid():N}.json");
        try
        {
            var home = SampleHome.Create();
            var editor = new HomeEditorViewModel(home);
            editor.BindSource("garden", new ObservationSource("sensor.soil", "Garden soil", [ObservationRole.Moisture]), ObservationRole.Moisture);
            var store = new LocalHomeStore(file);
            await store.SaveAsync(HomeProfile.From(editor.Build(), []), TestContext.Current.CancellationToken);
            var profile = Assert.IsType<HomeProfile>(await store.LoadAsync(TestContext.Current.CancellationToken));
            Assert.Equal(editor.Bindings, profile.ApplyTo(home).Bindings);
            var json = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
            Assert.DoesNotContain("Garden soil", json);
            Assert.DoesNotContain("21.4", json);
            Assert.DoesNotContain("Observations", json);
            Assert.Throws<ArgumentException>(() => (profile with { Bindings = [new("not-here", "sensor.soil", ObservationRole.Moisture)] }).Validate());
            Assert.Empty((profile with { Bindings = null }).ApplyTo(home).Bindings);
        }
        finally { if (File.Exists(file)) { File.Delete(file); } }
    }
}

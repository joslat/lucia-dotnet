using Lucia.Companion.App.Configuration;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class HomeImportTests
{
    [Fact]
    public void PreviewAddsOnlyChosenRoomsAndBindingsWithoutChangingExistingLayoutOrReadings()
    {
        var reading = new Observation("Temperature", "21 °C", ObservationQuality.Current, DateTimeOffset.UtcNow);
        var original = new HomeTopology("home-a", "Our home", [
            new("porch", "Front step", PlaceState.Quiet, "Reported", [reading]) { Icon = PlaceIcon.Garden },
            new("garden", "Garden", PlaceState.Unavailable, "Unknown", [])
        ], [new("porch", "garden")], [new("garden", "sensor.porch_temperature", ObservationRole.Temperature), new("porch", "sensor.missing", ObservationRole.Humidity)]);
        var plan = HomeImportPlan.Create(original, Catalog(), [new("porch", "Ignored replacement", PlaceIcon.Home), new("attic", "Office", PlaceIcon.Home)]);
        Assert.Equal(2, original.Places.Count);
        Assert.Equal(2, original.Bindings.Count);
        var preview = plan.Preview;
        var porch = Assert.Single(preview.Places, place => place.Id == "porch");
        Assert.Equal("Front step", porch.Name);
        Assert.Equal(PlaceIcon.Garden, porch.Icon);
        Assert.Same(reading, Assert.Single(porch.Observations));
        Assert.Equal(original.Connections, preview.Connections);
        var added = Assert.Single(preview.Places, place => place.Id == "attic");
        Assert.Equal("Office", added.Name);
        Assert.Equal(PlaceState.Unavailable, added.State);
        Assert.Empty(added.Observations);
        Assert.Contains("No readings", added.Reason);
        Assert.Equal(new[] { "attic" }, plan.AddedPlaceIds);
        Assert.Equal("sensor.attic_temperature", Assert.Single(plan.AddedBindings).SourceId);
        var conflict = Assert.Single(plan.Conflicts);
        Assert.Equal("garden", conflict.ExistingPlaceId);
        Assert.Equal("porch", conflict.ProposedPlaceId);
        Assert.Contains(preview.Bindings, binding => binding.SourceId == "sensor.missing");
        Assert.DoesNotContain(preview.Places, place => place.Id == "bedroom");
        Assert.Same(preview, plan.ApplyTo(original));
    }

    [Fact]
    public void ReimportIsIdempotentAndReviewCannotOverwriteAnEditedDraft()
    {
        var original = new HomeTopology("home-a", "Our home", [], []);
        var choices = new HomeImportChoice[] { new("attic", "Office", PlaceIcon.Home) };
        var first = HomeImportPlan.Create(original, Catalog(), choices);
        var repeated = HomeImportPlan.Create(first.Preview, Catalog(), choices);
        Assert.Empty(repeated.AddedPlaceIds);
        Assert.Empty(repeated.AddedBindings);
        Assert.Empty(repeated.Conflicts);
        Assert.Equal(first.Preview.Places, repeated.Preview.Places);
        Assert.Equal(first.Preview.Bindings, repeated.Preview.Bindings);
        var edited = new HomeTopology("home-a", "Edited home name", [], []);
        Assert.Throws<InvalidOperationException>(() => first.ApplyTo(edited));
        Assert.Equal("Edited home name", edited.Name);
    }

    [Theory]
    [InlineData("home")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("long-name")]
    [InlineData("capacity")]
    [InlineData("case")]
    public void InvalidSelectionFailsWithoutPartialChanges(string defect)
    {
        var original = new HomeTopology(defect == "home" ? "other-home" : "home-a", "Our home",
            defect == "capacity" ? Enumerable.Range(0, 200).Select(index => new Place("local-" + index, "Local place", PlaceState.Unavailable, "Unknown", [])).ToArray() : [], []);
        HomeImportChoice[] choices = defect switch
        {
            "missing" => [new("missing", "Missing", PlaceIcon.Home)],
            "case" => [new("ATTIC", "Attic", PlaceIcon.Home)],
            "duplicate" => [new("attic", "Office", PlaceIcon.Home), new("attic", "Again", PlaceIcon.Home)],
            "long-name" => [new("attic", new string('a', 81), PlaceIcon.Home)],
            _ => [new("attic", "Office", PlaceIcon.Home)]
        };
        var places = original.Places.ToArray();
        Assert.Throws<ArgumentException>(() => HomeImportPlan.Create(original, Catalog(), choices));
        Assert.Equal(places, original.Places);
        Assert.Empty(original.Bindings);
    }

    [Fact]
    public void EmptySelectionDoesNothingAndAnEmptyDiscoveredRoomDoesNotInventBindingsOrReadings()
    {
        var original = new HomeTopology("home-a", "Our home", [], []);
        Assert.Same(original, HomeImportPlan.Create(original, Catalog(), []).Preview);
        var catalog = new HomeCatalog("home-a", DateTimeOffset.UtcNow, [new("attic", "Attic", [])]);
        var plan = HomeImportPlan.Create(original, catalog, [new("attic", "Attic", PlaceIcon.Home)]);
        Assert.Empty(plan.AddedBindings);
        var place = Assert.Single(plan.Preview.Places);
        Assert.Empty(place.Observations);
        Assert.Contains("No observation bindings", place.Reason);
        Assert.Equal(PlaceState.Unavailable, place.State);
    }

    [Fact]
    public void BindingCapacityFailureDoesNotDiscardExistingMissingSourcesToMakeRoom()
    {
        var original = new HomeTopology("home-a", "Our home", [new("porch", "Porch", PlaceState.Unavailable, "Unknown", [])], [],
            Enumerable.Range(0, 2000).Select(index => new ObservationBinding("porch", "sensor.missing_" + index, ObservationRole.Temperature)));
        Assert.Throws<ArgumentException>(() => HomeImportPlan.Create(original, Catalog(), [new("attic", "Attic", PlaceIcon.Home)]));
        Assert.Equal(2000, original.Bindings.Count);
        Assert.Single(original.Places);
    }

    [Fact]
    public void ConnectedProfileNamesAreCanonicalAndIsolatedFromOtherHomesPeopleHostsAndTheDemo()
    {
        var connection = new ConnectionSession(new Uri("https://SYNTHETIC.invalid:443/"), CompanionHomeClientTests.Grant());
        var name = ConnectedProfileScope.FileName(connection);
        Assert.Matches("^connected-home-[0-9a-f]{64}\\.json$", name);
        Assert.Equal(name, ConnectedProfileScope.FileName(connection with { Endpoint = new Uri("https://synthetic.invalid") }));
        Assert.NotEqual(name, ConnectedProfileScope.FileName(connection with { Endpoint = new Uri("https://other.invalid") }));
        var otherMember = new DeviceSession(new string('b', 32), "Tablet", DateTimeOffset.UtcNow.AddDays(1), "home-a", "member-b", []);
        Assert.NotEqual(name, ConnectedProfileScope.FileName(connection with { Session = otherMember }));
        var sameMember = new DeviceSession(new string('c', 32), "Replacement phone", DateTimeOffset.UtcNow.AddDays(2), "home-a", "member-a", connection.Session.Places);
        Assert.Equal(name, ConnectedProfileScope.FileName(connection with { Session = sameMember }));
        var narrowed = new DeviceSession(new string('d', 32), "Phone", DateTimeOffset.UtcNow.AddDays(1), "home-a", "member-a", []);
        Assert.NotEqual(name, ConnectedProfileScope.FileName(connection with { Session = narrowed }));
        var otherHome = new DeviceSession(new string('c', 32), "Phone", DateTimeOffset.UtcNow.AddDays(1), "home-b", "member-a", []);
        Assert.NotEqual(name, ConnectedProfileScope.FileName(connection with { Session = otherHome }));
        Assert.NotEqual("offline-home.json", name);
        Assert.DoesNotContain("member-a", name);
        Assert.Throws<ArgumentException>(() => ConnectedProfileScope.FileName(connection with { Endpoint = new Uri("http://synthetic.invalid") }));
    }

    [Fact]
    public void ProfileScopeDependsOnHomeAccessNotGrantOrderingOrUnrelatedCapabilities()
    {
        var first = new DeviceSession(new string('a', 32), "Phone", DateTimeOffset.UtcNow.AddDays(1), "home-a", "member-a",
            [new("porch", [ObservationRole.Temperature, ObservationRole.Humidity]), new("garden", [ObservationRole.Moisture])]);
        var reordered = new DeviceSession(new string('b', 32), "Tablet", DateTimeOffset.UtcNow.AddDays(2), "home-a", "member-a",
            [new("garden", [ObservationRole.Moisture]), new("porch", [ObservationRole.Humidity, ObservationRole.Temperature])], canReadHistory: true);
        var origin = new Uri("https://synthetic.invalid");
        Assert.Equal(ConnectedProfileScope.FileName(new(origin, first)), ConnectedProfileScope.FileName(new(origin, reordered)));
        var narrowerFields = new DeviceSession(new string('c', 32), "Phone", DateTimeOffset.UtcNow.AddDays(1), "home-a", "member-a",
            [new("porch", [ObservationRole.Temperature]), new("garden", [ObservationRole.Moisture])]);
        Assert.NotEqual(ConnectedProfileScope.FileName(new(origin, first)), ConnectedProfileScope.FileName(new(origin, narrowerFields)));
    }

    private static HomeCatalog Catalog() => new("home-a", DateTimeOffset.UtcNow, [
        new("porch", "Porch", [new("sensor.porch_temperature", "Temperature", ObservationRole.Temperature)]),
        new("attic", "Attic", [new("sensor.attic_temperature", "Temperature", ObservationRole.Temperature)]),
        new("bedroom", "Bedroom", [new("sensor.bedroom_temperature", "Temperature", ObservationRole.Temperature)])
    ]);
}

using Lucia.Companion.App.Configuration;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class SharedLayoutAdoptionPlanTests
{
    [Fact]
    public void ReviewStagesExactSharedMetadataWithoutChangingLocalSetupOrReusingReadings()
    {
        var original = Original();
        var layout = Layout();
        var plan = SharedLayoutAdoptionPlan.Create(original, layout, Identity());
        Assert.Equal("Personal home", original.Name);
        Assert.Equal("Local porch", original.Places[0].Name);
        Assert.NotEmpty(original.Places[0].Observations);
        Assert.Equal("Shared home", plan.Preview.Name);
        Assert.Equal(new[] { "shared-porch", "shared-garden" }, plan.Preview.Places.Select(place => place.Id));
        Assert.Equal("Owner porch", plan.Preview.Places[0].Name);
        Assert.Equal(PlaceIcon.Door, plan.Preview.Places[0].Icon);
        Assert.All(plan.Preview.Places, place => { Assert.Equal(PlaceState.Unavailable, place.State); Assert.Empty(place.Observations); });
        Assert.Equal(new PlaceConnection("shared-porch", "shared-garden"), Assert.Single(plan.Preview.Connections));
        Assert.Equal(new ObservationBinding("shared-porch", "sensor.porch_temperature", ObservationRole.Temperature, "porch"), Assert.Single(plan.Preview.Bindings));
        Assert.Equal(layout.Revision, plan.Revision);
        Assert.True(plan.HasUnqualifiedBindings);
        Assert.Same(plan.Preview, plan.ApplyTo(original, layout, Identity()));
    }

    [Fact]
    public void ObservationRefreshDoesNotInvalidateReviewButAnEditedLayoutDoes()
    {
        var original = Original();
        var layout = Layout();
        var plan = SharedLayoutAdoptionPlan.Create(original, layout, Identity());
        var refreshed = new HomeTopology(original.Id, original.Name, original.Places.Select(place => place with
            { State = PlaceState.Changed, Reason = "New source report", Observations = [] }), original.Connections, original.Bindings.Reverse());
        Assert.Same(plan.Preview, plan.ApplyTo(refreshed, layout, Identity()));
        var edited = new HomeTopology(original.Id, "Changed draft", original.Places, original.Connections, original.Bindings);
        Assert.Throws<InvalidOperationException>(() => { plan.ApplyTo(edited, layout, Identity()); });
        Assert.Equal("Changed draft", edited.Name);
    }

    [Theory]
    [InlineData("home")]
    [InlineData("member")]
    [InlineData("hidden-place")]
    [InlineData("empty-fields")]
    [InlineData("hidden-field")]
    public void ForeignOrUnpermittedMetadataCannotBecomeAnAdoptionPlan(string defect)
    {
        var identity = Identity(defect);
        var original = Original();
        Assert.Throws<ArgumentException>(() => { SharedLayoutAdoptionPlan.Create(original, Layout(), identity); });
        Assert.Equal("Personal home", original.Name);
        Assert.Single(original.Places);
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("payload")]
    [InlineData("review-flag")]
    [InlineData("endpoint")]
    [InlineData("session")]
    [InlineData("grant")]
    public void ChangedSharedVersionIdentityOrHomeGrantRequiresAnotherReview(string defect)
    {
        var original = Original();
        var plan = SharedLayoutAdoptionPlan.Create(original, Layout(), Identity());
        var current = Layout(revision: defect == "revision" ? new string('B', 64) : null,
            name: defect == "payload" ? "Changed owner home" : "Shared home", unqualified: defect != "review-flag");
        var identity = Identity(defect);
        Assert.Throws<InvalidOperationException>(() => { plan.ApplyTo(original, current, identity); });
        Assert.Equal("Personal home", original.Name);
    }

    [Fact]
    public void EquivalentGrantOrderingAndRenewedExpiryDoNotInvalidateReview()
    {
        var original = Original();
        var layout = Layout();
        var identity = Identity();
        var plan = SharedLayoutAdoptionPlan.Create(original, layout, identity);
        var renewed = new DeviceSession(identity.Session.SessionId, "Renamed phone", DateTimeOffset.UtcNow.AddDays(5), "home-a", "member-a",
            identity.Session.Places.Reverse().ToArray(), canReadActivity: true);
        Assert.Same(plan.Preview, plan.ApplyTo(original, layout, new(new Uri("https://LOCALHOST:443/"), renewed)));
    }

    [Fact]
    public void ChangedUnboundPlaceProvenanceCannotReuseAnInspectedRevision()
    {
        var original = Original();
        var layout = Layout();
        var plan = SharedLayoutAdoptionPlan.Create(original, layout, Identity());
        var changed = new SharedHomeLayout(layout.HomeId, layout.MemberId, layout.Revision, layout.CapturedAt,
            layout.Name, layout.HasUnqualifiedBindings,
            [layout.Places[0], layout.Places[1] with { SourcePlaceId = "porch" }], layout.Connections, layout.Bindings);
        Assert.Throws<InvalidOperationException>(() => { plan.ApplyTo(original, changed, Identity()); });
    }

    [Theory]
    [InlineData("name")]
    [InlineData("icon")]
    [InlineData("order")]
    [InlineData("connection")]
    [InlineData("binding")]
    [InlineData("binding-provenance")]
    public void LocalMetadataEditsCannotBeSilentlyReplacedByAnEarlierReview(string defect)
    {
        var original = SharedLayoutAdoptionPlan.Create(Original(), Layout(), Identity()).Preview;
        var plan = SharedLayoutAdoptionPlan.Create(original, Layout(), Identity());
        var places = original.Places.ToArray();
        if (defect == "name") { places[0] = places[0] with { Name = "Personal label" }; }
        if (defect == "icon") { places[0] = places[0] with { Icon = PlaceIcon.Home }; }
        if (defect == "order") { Array.Reverse(places); }
        var bindings = defect == "binding" ? [] : original.Bindings.ToArray();
        if (defect == "binding-provenance") { bindings[0] = new(bindings[0].PlaceId, bindings[0].SourceId, bindings[0].Role, "garden"); }
        var edited = new HomeTopology(original.Id, original.Name, places, defect == "connection" ? [] : original.Connections, bindings);
        Assert.Throws<InvalidOperationException>(() => { plan.ApplyTo(edited, Layout(), Identity()); });
    }

    [Fact]
    public async Task MaximumSharedLabelsRoundTripThroughLocalMetadataWithoutTruncation()
    {
        var label = new string('a', 198) + "\U0001F33F";
        var plan = SharedLayoutAdoptionPlan.Create(Original(), Layout(name: label, placeName: label), Identity());
        var path = Path.Combine(Path.GetTempPath(), $"lucia-adoption-{Guid.NewGuid():N}.json");
        try
        {
            var store = new LocalHomeStore(path);
            await store.SaveAsync(HomeProfile.From(plan.Preview, []), null, TestContext.Current.CancellationToken);
            var restored = Assert.IsType<HomeProfile>(await store.LoadAsync(TestContext.Current.CancellationToken));
            Assert.Equal(label, restored.HomeName);
            Assert.Equal(label, restored.Places[0].Name);
            Assert.Equal(plan.Preview.Bindings, restored.Bindings);
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
            if (File.Exists(path + ".lock")) { File.Delete(path + ".lock"); }
        }
    }

    [Fact]
    public void LocalProfileStillRejectsNamesBeyondTheSharedMetadataLimit()
    {
        var profile = HomeProfile.From(new HomeTopology("home-a", new string('a', 201), [], []), []);
        Assert.Throws<ArgumentException>(profile.Validate);
    }

    private static HomeTopology Original() => new("home-a", "Personal home",
        [new("local-porch", "Local porch", PlaceState.Quiet, "Prior reading",
            [new("Temperature", "19 C", ObservationQuality.Current, DateTimeOffset.UtcNow)])], [],
        [new("local-porch", "sensor.porch_temperature", ObservationRole.Temperature, "porch")]);

    private static SharedHomeLayout Layout(string? revision = null, string name = "Shared home", bool unqualified = true, string placeName = "Owner porch") =>
        new("home-a", "member-a", revision ?? new string('A', 64), DateTimeOffset.UtcNow, name, unqualified,
            [new("shared-porch", placeName, PlaceIcon.Door, "porch"), new("shared-garden", "Garden", PlaceIcon.Garden, "garden")],
            [new("shared-porch", "shared-garden")], [new("shared-porch", "porch", "sensor.porch_temperature", ObservationRole.Temperature)]);

    private static ConnectionSession Identity(string? defect = null) => new(new Uri(defect == "endpoint" ? "https://other.invalid/" : "https://localhost/"),
        new DeviceSession(new string(defect == "session" ? 'b' : 'a', 32), "Phone", DateTimeOffset.UtcNow.AddDays(1),
            defect == "home" ? "other-home" : "home-a", defect == "member" ? "other-member" : "member-a",
            [new("porch", defect == "empty-fields" ? [] : [defect == "hidden-field" ? ObservationRole.Contact : ObservationRole.Temperature]),
                new(defect == "hidden-place" ? "other-place" : "garden", defect == "grant" ? [ObservationRole.Motion, ObservationRole.Contact] : [ObservationRole.Motion])]));
}

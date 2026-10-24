using System.Text.Json;
using Lucia.Companion.App.Configuration;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class ConnectedHomeProjectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConfiguredBindingShowsTypedReportedDataWithoutRenamingRoomsOrInventingPhysicalOutcomes()
    {
        var snapshot = Snapshot("porch", ObservationRole.Contact, new("binary_sensor.door", ObservationRole.Contact, ObservationQuality.Current, Now, null, false, null));
        var setup = Setup("binary_sensor.door", ObservationRole.Contact, "porch");
        var projected = ConnectedHomeProjection.Project(setup, snapshot, TimeSpan.Zero);
        var place = Assert.Single(projected.Places);
        Assert.Equal("Front step", place.Name);
        Assert.Equal(PlaceIcon.Door, place.Icon);
        var observation = Assert.Single(place.Observations);
        Assert.Equal("Closed", observation.CurrentText);
        Assert.StartsWith("Reported ", observation.TimestampText);
        Assert.DoesNotContain("locked", place.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("safe", place.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(setup.Bindings, projected.Bindings);
    }

    [Theory]
    [InlineData("garden")]
    [InlineData(null)]
    public void MovedOrUnqualifiedBindingCannotBorrowAValueFromAnotherSourceLocation(string? expectedLocation)
    {
        var snapshot = Snapshot("porch", ObservationRole.Temperature, new("sensor.temperature", ObservationRole.Temperature, ObservationQuality.Current, Now, 21.5, null, "°C"));
        var place = Assert.Single(ConnectedHomeProjection.Project(Setup("sensor.temperature", ObservationRole.Temperature, expectedLocation), snapshot, TimeSpan.Zero).Places);
        Assert.Equal(PlaceState.Unavailable, place.State);
        Assert.Equal("Unavailable", Assert.Single(place.Observations).CurrentText);
        Assert.Null(Assert.Single(place.Observations).Value);
        Assert.Contains("review", place.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CurrentReportsAgeFromTheServerCaptureWithoutUsingThePhoneWallClock()
    {
        var snapshot = Snapshot("porch", ObservationRole.Temperature, new("sensor.temperature", ObservationRole.Temperature, ObservationQuality.Current, Now.AddMinutes(-30), 21.5, null, "°C"));
        var setup = Setup("sensor.temperature", ObservationRole.Temperature, "porch");
        Assert.Equal(ObservationQuality.Current, Assert.Single(Assert.Single(ConnectedHomeProjection.Project(setup, snapshot, TimeSpan.Zero).Places).Observations).Quality);
        var old = Assert.Single(Assert.Single(ConnectedHomeProjection.Project(setup, snapshot, TimeSpan.FromSeconds(1)).Places).Observations);
        Assert.Equal(ObservationQuality.Stale, old.Quality);
        Assert.Equal("Stale", old.CurrentText);
        Assert.Contains("21", old.PreviousText);
        Assert.StartsWith("Reported ", old.TimestampText);
        Assert.Throws<ArgumentOutOfRangeException>(() => ConnectedHomeProjection.Project(setup, snapshot, TimeSpan.FromSeconds(-1)));
    }

    [Theory]
    [InlineData(ObservationQuality.Unknown)]
    [InlineData(ObservationQuality.Unavailable)]
    public void MissingReportNeverBecomesOffClosedOrZero(ObservationQuality quality)
    {
        var snapshot = Snapshot("porch", ObservationRole.Contact, new("binary_sensor.door", ObservationRole.Contact, quality, Now, null, null, null));
        var observation = Assert.Single(Assert.Single(ConnectedHomeProjection.Project(Setup("binary_sensor.door", ObservationRole.Contact, "porch"), snapshot, TimeSpan.Zero).Places).Observations);
        Assert.Equal(quality, observation.Quality);
        Assert.Null(observation.Value);
        Assert.Equal(string.Empty, observation.PreviousText);
    }

    [Fact]
    public void SourceLocationMetadataSurvivesImportManualBindingAndProfileRoundTripWithoutReportPersistence()
    {
        var setup = new HomeTopology("home-a", "Our home", [], []);
        var catalog = new HomeCatalog("home-a", Now, [new("porch", "Porch", [new("sensor.temperature", "Temperature", ObservationRole.Temperature)])]);
        var imported = HomeImportPlan.Create(setup, catalog, [new("porch", "Porch", PlaceIcon.Door)]).Preview;
        Assert.Equal("porch", Assert.Single(imported.Bindings).SourcePlaceId);
        var json = JsonSerializer.Serialize(HomeProfile.From(imported, []));
        Assert.DoesNotContain("reportedAt", json);
        var profile = JsonSerializer.Deserialize<HomeProfile>(json)!;
        profile.Validate();
        Assert.Equal("porch", Assert.Single(profile.ApplyTo(setup).Bindings).SourcePlaceId);
        var editor = new HomeEditorViewModel(Setup("sensor.existing", ObservationRole.Humidity, "porch"));
        editor.BindSource("local-porch", new("sensor.other", "Other", [ObservationRole.Temperature], "garden"), ObservationRole.Temperature);
        Assert.Equal("garden", Assert.Single(editor.Bindings, binding => binding.SourceId == "sensor.other").SourcePlaceId);
    }

    [Fact]
    public void AnotherHomeIsRejectedBeforeAnyProjection()
    {
        var snapshot = Snapshot("porch", ObservationRole.Contact, new("binary_sensor.door", ObservationRole.Contact, ObservationQuality.Current, Now, null, true, null));
        Assert.Throws<ArgumentException>(() => ConnectedHomeProjection.Project(new("other-home", "Other", [], []), snapshot, TimeSpan.Zero));
    }

    private static HomeTopology Setup(string sourceId, ObservationRole role, string? sourcePlaceId) => new("home-a", "Our home",
        [new("local-porch", "Front step", PlaceState.Unavailable, "Unknown", []) { Icon = PlaceIcon.Door }], [], [new("local-porch", sourceId, role, sourcePlaceId)]);
    private static HomeSnapshot Snapshot(string sourcePlace, ObservationRole field, HomeReading reading) => new("home-a", Now, 1800,
        [new(sourcePlace, "Source place", [new(reading.SourceId, "Source report", field)], [reading])]);
}

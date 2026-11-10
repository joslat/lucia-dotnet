using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class PlaceCaptionTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private static Observation Reading(string label, string? value, ObservationRole? role, ObservationQuality quality = ObservationQuality.Current, DateTimeOffset? at = null) =>
        new(label, value, quality, at ?? (quality == ObservationQuality.Current ? Noon : null)) { Role = role };

    private static Place Place(PlaceState state, params Observation[] readings) => new("p", "Place", state, "Reason.", readings);

    [Fact]
    public void CaptionLeadsWithLightsThenTemperature()
    {
        var place = Place(PlaceState.Quiet, Reading("Temperature", "22.0 °C", ObservationRole.Temperature), Reading("Lighting", "Off", ObservationRole.Light));
        Assert.Equal("Lights off · 22.0 °C", PlaceCaption.Caption(place));
        Assert.Null(PlaceCaption.Status(place));
    }

    [Fact]
    public void ContactsNameTheirOpeningAndCaptionsKeepTwoReadings()
    {
        Assert.Equal("Front door closed", PlaceCaption.Caption(Place(PlaceState.Quiet, Reading("Front door contact", "Closed", ObservationRole.Contact))));
        Assert.Equal("Hall door open", PlaceCaption.Caption(Place(PlaceState.Changed, Reading("Hall door · Contact", "Open", ObservationRole.Contact))));
        var busy = Place(PlaceState.Quiet,
            Reading("Temperature", "20 °C", ObservationRole.Temperature), Reading("Gate contact", "Open", ObservationRole.Contact), Reading("Lighting", "On", ObservationRole.Light));
        Assert.Equal("Lights on · Gate open", PlaceCaption.Caption(busy));
    }

    [Fact]
    public void OnlyCurrentReadingsAppearAndUntypedOnesKeepTheirLabel()
    {
        var place = Place(PlaceState.Unavailable,
            Reading("Humidity", "65 %", ObservationRole.Humidity, ObservationQuality.Stale, Noon.AddHours(-1)), Reading("Pressure", "1013 hPa", null));
        Assert.Equal("Pressure 1013 hPa", PlaceCaption.Caption(place));
        Assert.Equal("Some data unavailable", PlaceCaption.Status(place));
    }

    [Fact]
    public void UnavailablePlacesSayWhenDataStopped()
    {
        var silent = Place(PlaceState.Unavailable, Reading("Temperature", "19.2 °C", ObservationRole.Temperature, ObservationQuality.Unavailable, Noon.AddHours(-2)));
        Assert.Null(PlaceCaption.Caption(silent));
        Assert.Equal("No data since 10:00", PlaceCaption.Status(silent));
        Assert.Equal("No data", PlaceCaption.Status(Place(PlaceState.Unavailable)));
    }

    [Theory]
    [InlineData(PlaceState.Changed, "Changed · 20 °C")]
    [InlineData(PlaceState.Working, "Lucia is checking")]
    [InlineData(PlaceState.Waiting, "Waiting for you")]
    [InlineData(PlaceState.Problem, "Reported problem")]
    public void StatusesUseTheDesignWording(PlaceState state, string expected) =>
        Assert.Equal(expected, PlaceCaption.Status(Place(state, Reading("Temperature", "20 °C", ObservationRole.Temperature))));

    [Fact]
    public void AChangedPlaceWithoutCurrentReadingsJustSaysChanged() => Assert.Equal("Changed", PlaceCaption.Status(Place(PlaceState.Changed)));

    [Fact]
    public void AQuietPlaceWithNothingToSummariseStillSaysQuiet()
    {
        var place = Place(PlaceState.Quiet);
        Assert.Null(PlaceCaption.Caption(place));
        Assert.Equal("Quiet", PlaceCaption.Status(place));
    }
}

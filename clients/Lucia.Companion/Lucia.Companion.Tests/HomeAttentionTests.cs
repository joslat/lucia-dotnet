using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class HomeAttentionTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "Nothing needs you")]
    [InlineData(1, "1 thing needs you")]
    [InlineData(3, "3 things need you")]
    public void HeadlineCountsThingsThatNeedYou(int count, string expected) => Assert.Equal(expected, HomeAttention.Headline(count));

    [Theory]
    [InlineData(7, 3, "Garage first. The other 4 places have nothing to report.")]
    [InlineData(4, 3, "Garage first. The other place has nothing to report.")]
    [InlineData(3, 3, "Garage first.")]
    public void SublineNamesTheFirstPlaceAndCountsTheRest(int places, int attention, string expected) =>
        Assert.Equal(expected, HomeAttention.Subline("Garage", attention, places, isDemo: true));

    [Fact]
    public void SublineWithNothingFlaggedMakesNoSafetyClaim()
    {
        Assert.Equal("No places are set up yet.", HomeAttention.Subline(null, 0, 0, isDemo: false));
        Assert.Equal("None of the 6 places in this sample has anything to report.", HomeAttention.Subline(null, 0, 6, isDemo: true));
        Assert.Equal("Your configured reports flag nothing. That isn't a safety or presence check.", HomeAttention.Subline(null, 0, 6, isDemo: false));
    }

    [Theory]
    [InlineData(PlaceState.Problem, "Reported problem")]
    [InlineData(PlaceState.Waiting, "Waiting for you")]
    [InlineData(PlaceState.Unavailable, "Data unavailable")]
    [InlineData(PlaceState.Quiet, "Quiet")]
    public void KindLabelsMatchTheDesign(PlaceState state, string expected) => Assert.Equal(expected, HomeAttention.KindLabel(state));

    [Theory]
    [InlineData(0, "just now")]
    [InlineData(-30, "just now")]
    [InlineData(45, "just now")]
    [InlineData(60, "1 min ago")]
    [InlineData(59 * 60, "59 min ago")]
    [InlineData(90 * 60, "1 h 30 min ago")]
    [InlineData(3 * 3600, "3 h ago")]
    [InlineData(2 * 86400 + 5, "2 days ago")]
    [InlineData(86400, "1 day ago")]
    public void AgeIsMeasuredFromTheReportTime(int secondsBefore, string expected) =>
        Assert.Equal(expected, HomeAttention.Age(Noon.AddSeconds(-secondsBefore), Noon));

    [Fact]
    public void UnknownTimesHaveNoAge()
    {
        Assert.Null(HomeAttention.Age(null, Noon));
        Assert.Null(HomeAttention.Age(Noon, null));
    }

    [Fact]
    public void ReadingLineUsesThePlacesLatestTimedReading()
    {
        var place = new Place("garage", "Garage", PlaceState.Problem, "Freezer plug reported a fault.",
        [
            new("Plug", "Fault", ObservationQuality.Current, Noon.AddMinutes(-3)),
            new("Door", "Closed", ObservationQuality.Current, Noon.AddMinutes(-40)),
            new("Temperature", null, ObservationQuality.Unknown, null),
        ]);
        Assert.Equal(Noon.AddMinutes(-3), HomeAttention.LatestReadingAt(place));
        Assert.Equal("Latest reading 3 min ago", HomeAttention.ReadingLine(place, Noon));
        Assert.Equal("No reading time", HomeAttention.ReadingLine(place with { Observations = [] }, Noon));
        Assert.Equal("No reading time", HomeAttention.ReadingLine(place, null));
    }
}

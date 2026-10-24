using lucia.Agents.Companion;

namespace lucia.Tests.Auth;

public sealed class CompanionReadingMapperTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("21.5", "°C", 21.5)]
    [InlineData("68", "°F", 68.0)]
    [InlineData("294.15", "K", 294.15)]
    [InlineData("NaN", "°C", null)]
    [InlineData("Infinity", "°C", null)]
    [InlineData("1,5", "°C", null)]
    [InlineData("21", "private-unit", null)]
    [InlineData("-300", "°C", null)]
    public void NumericReportsNeedFiniteInvariantValuesAndKnownUnits(string value, string unit, double? expected)
    {
        var state = CompanionHomeApiTests.State("sensor.temperature", value, "temperature", unit);
        state.LastReported = Now;
        var result = CompanionReadingMapper.Read(state, CompanionField.Temperature, Now, TimeSpan.FromMinutes(30));
        Assert.Equal(expected, result.NumberValue);
        Assert.Equal(expected.HasValue ? CompanionReadingQuality.Current : CompanionReadingQuality.Unknown, result.Quality);
        Assert.Null(result.IsActive);
    }

    [Fact]
    public void UnchangedDoorUsesReportTimeAndNeverBecomesLockedOrOccupied()
    {
        var state = CompanionHomeApiTests.State("binary_sensor.door", "off", "door");
        state.LastUpdated = Now.AddDays(-3).UtcDateTime;
        state.LastChanged = state.LastUpdated;
        state.LastReported = Now;
        Assert.Equal(CompanionField.Contact, CompanionReadingMapper.Field(state));
        var reading = CompanionReadingMapper.Read(state, CompanionField.Contact, Now, TimeSpan.FromMinutes(30));
        Assert.Equal(CompanionReadingQuality.Current, reading.Quality);
        Assert.False(reading.IsActive);
        Assert.Equal(Now, reading.ReportedAt);
        state.Attributes["device_class"] = "lock";
        Assert.Null(CompanionReadingMapper.Field(state));
        state.Attributes["device_class"] = "occupancy";
        Assert.Null(CompanionReadingMapper.Field(state));
    }

    [Theory]
    [InlineData(-1800, "Current")]
    [InlineData(-1801, "Stale")]
    [InlineData(1, "Unknown")]
    public void FreshnessHasAnExactBoundaryAndFutureReportsCannotBorrowOldTime(int offsetSeconds, string expected)
    {
        var state = CompanionHomeApiTests.State("sensor.temperature", "21", "temperature", "°C");
        state.LastUpdated = Now.UtcDateTime;
        state.LastReported = Now.AddSeconds(offsetSeconds);
        var reading = CompanionReadingMapper.Read(state, CompanionField.Temperature, Now, TimeSpan.FromMinutes(30));
        Assert.Equal(expected, reading.Quality.ToString());
        Assert.Equal(expected == "Unknown" ? null : (double?)21, reading.NumberValue);
    }

    [Theory]
    [InlineData("unknown", "Unknown")]
    [InlineData("unavailable", "Unavailable")]
    public void MissingReportsNeverTurnIntoFalseOrZero(string value, string expected)
    {
        var state = CompanionHomeApiTests.State("binary_sensor.motion", value, "motion");
        state.LastReported = Now;
        var reading = CompanionReadingMapper.Read(state, CompanionField.Motion, Now, TimeSpan.FromMinutes(30));
        Assert.Equal(expected, reading.Quality.ToString());
        Assert.Null(reading.IsActive);
        Assert.Null(reading.NumberValue);
    }

    [Fact]
    public void OlderHostsUseAnExplicitTimestampFallbackButNeverInventOne()
    {
        var state = CompanionHomeApiTests.State("sensor.temperature", "21", "temperature", "°C");
        state.LastUpdated = Now.UtcDateTime;
        Assert.Equal(Now, CompanionReadingMapper.Read(state, CompanionField.Temperature, Now, TimeSpan.FromMinutes(30)).ReportedAt);
        state.LastUpdated = DateTime.SpecifyKind(Now.DateTime, DateTimeKind.Unspecified);
        var untimed = CompanionReadingMapper.Read(state, CompanionField.Temperature, Now, TimeSpan.FromMinutes(30));
        Assert.Equal(CompanionReadingQuality.Unknown, untimed.Quality);
        Assert.Null(untimed.ReportedAt);
        Assert.Null(untimed.NumberValue);
    }
}

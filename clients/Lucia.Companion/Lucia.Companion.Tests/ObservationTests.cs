using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class ObservationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ObservationQuality.Unknown, "Unknown")]
    [InlineData(ObservationQuality.Unavailable, "Unavailable")]
    [InlineData(ObservationQuality.Stale, "Stale")]
    public void UnqualifiedValueIsNeverPresentedAsCurrent(ObservationQuality quality, string label)
    {
        var observation = new Observation("Temperature", "21.4 °C", quality, Now.AddMinutes(-20));
        Assert.Equal(label, observation.CurrentText);
        Assert.Contains("21.4 °C", observation.PreviousText);
    }

    [Fact]
    public void MissingValueDoesNotBecomeZero()
    {
        var observation = new Observation("Temperature", null, ObservationQuality.Current, Now);
        Assert.Equal("Unknown", observation.CurrentText);
        Assert.Equal(string.Empty, observation.PreviousText);
    }

    [Fact]
    public void ValueWithoutObservationTimeIsNotCurrent()
    {
        var observation = new Observation("Door", "Closed", ObservationQuality.Current, null);
        Assert.Equal("Unknown", observation.CurrentText);
    }

    [Fact]
    public void QualifiedObservationPreservesSourceMeaning()
    {
        var observation = new Observation("Door", "Closed", ObservationQuality.Current, Now);
        Assert.Equal("Closed", observation.CurrentText);
        Assert.DoesNotContain("Locked", observation.CurrentText);
        Assert.Equal(string.Empty, observation.PreviousText);
    }

    [Fact]
    public void LabelsCannotBeBlank()
    {
        Assert.Throws<ArgumentException>(() => new Observation(" ", "0", ObservationQuality.Current, Now));
    }
}

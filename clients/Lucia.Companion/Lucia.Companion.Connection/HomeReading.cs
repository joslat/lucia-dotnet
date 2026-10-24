using Lucia.Companion.Core;

namespace Lucia.Companion.Connection;

/// <summary>A typed source report, not independent physical observation or control authority.</summary>
public sealed class HomeReading
{
    public HomeReading(string sourceId, ObservationRole field, ObservationQuality quality, DateTimeOffset? reportedAt,
        double? numberValue, bool? isActive, string? unit)
    {
        SourceId = WireValue.SourceIdentifier(sourceId);
        if (!Enum.IsDefined(field) || !Enum.IsDefined(quality) || reportedAt < DateTimeOffset.UnixEpoch)
        { throw new ArgumentException("The source report is invalid."); }
        var known = quality is ObservationQuality.Current or ObservationQuality.Stale;
        var validValue = numberValue is { } number
            ? isActive is null && double.IsFinite(number) && ValidNumber(field, unit, number)
            : isActive.HasValue && unit is null && field is ObservationRole.Contact or ObservationRole.Motion or ObservationRole.Light or ObservationRole.Leak;
        if (known ? !reportedAt.HasValue || !validValue : numberValue.HasValue || isActive.HasValue || unit is not null)
        { throw new ArgumentException("The report quality and value disagree."); }
        Field = field; Quality = quality; ReportedAt = reportedAt; NumberValue = numberValue; IsActive = isActive; Unit = unit;
    }
    public string SourceId { get; }
    public ObservationRole Field { get; }
    public ObservationQuality Quality { get; }
    public DateTimeOffset? ReportedAt { get; }
    public double? NumberValue { get; }
    public bool? IsActive { get; }
    public string? Unit { get; }

    private static bool ValidNumber(ObservationRole field, string? unit, double value) => field switch
    {
        ObservationRole.Temperature => unit switch { "°C" => value >= -273.15, "°F" => value >= -459.67, "K" => value >= 0, _ => false },
        ObservationRole.Humidity or ObservationRole.Moisture => unit == "%" && value is >= 0 and <= 100,
        ObservationRole.Light => unit == "lx" && value >= 0,
        _ => false
    };
}

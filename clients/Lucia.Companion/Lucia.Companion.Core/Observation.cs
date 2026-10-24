using System.Globalization;

namespace Lucia.Companion.Core;

/// <summary>A source-qualified reading. Freshness must be qualified by the data boundary, not guessed by the view.</summary>
public sealed record Observation
{
    public Observation(string label, string? value, ObservationQuality quality, DateTimeOffset? observedAt, ObservationTimeBasis timeBasis = ObservationTimeBasis.Observed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (!Enum.IsDefined(quality))
        {
            throw new ArgumentOutOfRangeException(nameof(quality));
        }
        if (!Enum.IsDefined(timeBasis)) { throw new ArgumentOutOfRangeException(nameof(timeBasis)); }

        Label = label;
        Value = string.IsNullOrWhiteSpace(value) ? null : value;
        Quality = quality;
        ObservedAt = observedAt;
        TimeBasis = timeBasis;
    }

    public string Label { get; }
    public string? Value { get; }
    public ObservationQuality Quality { get; }
    public DateTimeOffset? ObservedAt { get; }
    public ObservationTimeBasis TimeBasis { get; }
    private bool IsCurrent => Quality == ObservationQuality.Current && Value is not null && ObservedAt.HasValue;
    public string CurrentText => IsCurrent ? Value! : Quality switch
    {
        ObservationQuality.Stale => "Stale",
        ObservationQuality.Unavailable => "Unavailable",
        _ => "Unknown"
    };

    public string PreviousText => !IsCurrent && Value is not null
        ? $"Last known: {Value}. {TimestampText}"
        : string.Empty;

    public string TimestampText => ObservedAt is { } time
        ? $"{TimeBasis} {time.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)}"
        : TimeBasis == ObservationTimeBasis.Reported ? "Report time unknown" : "Observation time unknown";
}

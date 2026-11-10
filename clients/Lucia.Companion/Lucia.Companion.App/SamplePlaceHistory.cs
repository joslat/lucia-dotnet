using System.Globalization;
using System.Text.RegularExpressions;
using Lucia.Companion.Core;

namespace Lucia.Companion.App;

/// <summary>
/// Authored, deterministic sample series for the demo's place charts. Not history: it is drawn only in the demo, is labelled
/// as a sample, and ends at the reading's own time so a silent source shows a gap instead of an invented value.
/// </summary>
public static partial class SamplePlaceHistory
{
    /// <summary>The first timed reading whose value starts with a number, with that number and its unit.</summary>
    public static (Observation Observation, double Value, string Unit)? Chartable(Place place)
    {
        ArgumentNullException.ThrowIfNull(place);
        foreach (var observation in place.Observations)
        {
            if (observation.Value is null || !observation.ObservedAt.HasValue) { continue; }
            var match = LeadingNumber().Match(observation.Value);
            if (!match.Success) { continue; }
            return (observation, double.Parse(match.Value, CultureInfo.InvariantCulture), observation.Value[match.Length..].Trim());
        }
        return null;
    }

    /// <summary>Half-hourly points from the start of the day to the reading's time, ending exactly on the reading.</summary>
    public static IReadOnlyList<ReadingPoint> Series(string label, double finalValue, DateTimeOffset dayStart, DateTimeOffset lastAt)
    {
        var seed = label.Sum(character => character) % 17;
        var step = TimeSpan.FromMinutes(30);
        var points = new List<ReadingPoint>();
        for (var at = dayStart; at < lastAt; at += step)
        {
            var hours = (at - dayStart).TotalHours;
            var drift = Math.Sin((hours + seed) / 24 * Math.PI * 2) * 1.2 + Math.Sin(hours * 1.3 + seed) * 0.3;
            var weight = Math.Clamp((lastAt - at).TotalHours / 3, 0, 1);
            points.Add(new(at, Math.Round(finalValue + drift * weight, 1)));
        }
        points.Add(new(lastAt, finalValue));
        return points.AsReadOnly();
    }

    [GeneratedRegex(@"^-?\d+(\.\d+)?")]
    private static partial Regex LeadingNumber();
}

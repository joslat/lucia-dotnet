using System.Globalization;

namespace Lucia.Companion.Core;

/// <summary>
/// The line under a place on Home. A quiet place shows a summary of its current readings ("Lights off · 22.0 °C"); any other
/// place shows its status instead ("Waiting for you"), and a changed place says what changed ("Changed · Gate open").
/// Only current readings are summarised; stale or missing ones never read as now.
/// </summary>
public static class PlaceCaption
{
    private static readonly ObservationRole?[] Preference =
        [ObservationRole.Light, ObservationRole.Contact, ObservationRole.Temperature, ObservationRole.Leak, ObservationRole.Motion, ObservationRole.Humidity, ObservationRole.Moisture, null];

    public static string? Caption(Place place)
    {
        ArgumentNullException.ThrowIfNull(place);
        var current = place.Observations.Where(item => item.IsCurrent).ToArray();
        if (current.Length == 0) { return null; }
        var chosen = current.OrderBy(item => Array.IndexOf(Preference, item.Role)).Take(2).Select(Phrase);
        return string.Join(" · ", chosen);
    }

    /// <summary>For a changed place: its current contact and motion readings, which are what mark a place changed; otherwise its caption.</summary>
    public static string? WhatChanged(Place place)
    {
        ArgumentNullException.ThrowIfNull(place);
        var changes = place.Observations.Where(item => item.IsCurrent && item.Role is ObservationRole.Contact or ObservationRole.Motion).Take(2).Select(Phrase).ToArray();
        return changes.Length > 0 ? string.Join(" · ", changes) : Caption(place);
    }

    /// <summary>When what <see cref="WhatChanged"/> describes was reported: its newest contact or motion reading, else the newest reading.</summary>
    public static DateTimeOffset? WhatChangedAt(Place place)
    {
        ArgumentNullException.ThrowIfNull(place);
        var changes = place.Observations.Where(item => item.IsCurrent && item.Role is ObservationRole.Contact or ObservationRole.Motion).Take(2).ToArray();
        return changes.Length > 0 ? changes.Max(item => item.ObservedAt!.Value) : HomeAttention.LatestReadingAt(place);
    }

    public static string? Status(Place place)
    {
        ArgumentNullException.ThrowIfNull(place);
        return place.State switch
        {
            PlaceState.Quiet => Caption(place) is null ? "Quiet" : null,
            PlaceState.Changed => WhatChanged(place) is { } changed ? $"Changed · {changed}" : "Changed",
            PlaceState.Working => "Lucia is checking",
            PlaceState.Waiting => "Waiting for you",
            PlaceState.Problem => "Reported problem",
            PlaceState.Unavailable when place.Observations.Any(item => item.IsCurrent) => "Some data unavailable",
            PlaceState.Unavailable => HomeAttention.LatestReadingAt(place) is { } last
                ? $"No data since {last.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture)} UTC" : "No data",
            _ => place.State.ToString(),
        };
    }

    private static string Phrase(Observation item)
    {
        var value = item.Value!;
        return item.Role switch
        {
            ObservationRole.Temperature => value,
            ObservationRole.Light => $"Lights {value.ToLowerInvariant()}",
            _ => $"{Subject(item.Label)} {(item.Role is null ? value : value.ToLowerInvariant())}",
        };
    }

    /// <summary>"Front door contact" and "Hall door · Contact" both become the thing being described: "Front door", "Hall door".</summary>
    private static string Subject(string label)
    {
        var subject = label.Split(" · ")[0].Trim();
        foreach (var suffix in new[] { " contact", " sensor" })
        {
            if (subject.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && subject.Length > suffix.Length) { subject = subject[..^suffix.Length]; }
        }
        return subject;
    }
}

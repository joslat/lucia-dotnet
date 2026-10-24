using Lucia.Companion.Core;

namespace Lucia.Companion.App;

/// <summary>Static, synthetic examples only. No device discovery, inferred occupancy or service calls.</summary>
internal static class WorkbenchFixtures
{
    private static readonly DateTimeOffset SampleTime = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    public static IReadOnlyList<Place> Places { get; } =
    [
        new("living", "Living room", PlaceState.Quiet, "No recent change in this sample.",
            [new("Temperature", "21.4 °C", ObservationQuality.Current, SampleTime), new("Lighting", "Off", ObservationQuality.Current, SampleTime)]) { Icon = PlaceIcon.Sofa },
        new("garden", "Garden", PlaceState.Changed, "A contact reading changed in this sample.",
            [new("Gate contact", "Open", ObservationQuality.Current, SampleTime), new("Soil moisture", null, ObservationQuality.Unknown, null)]) { Icon = PlaceIcon.Garden },
        new("kitchen", "Kitchen", PlaceState.Working, "Lucia is reading a sensor in this sample. No physical action.",
            [new("Temperature", "20.8 °C", ObservationQuality.Current, SampleTime)]) { Icon = PlaceIcon.Kitchen },
        new("porch", "Porch", PlaceState.Waiting, "A sample request is waiting for a response.",
            [new("Front door contact", "Closed", ObservationQuality.Current, SampleTime), new("Door lock", null, ObservationQuality.Unknown, null)]) { Icon = PlaceIcon.Door },
        new("bedroom", "Bedroom", PlaceState.Unavailable, "The sample source is unavailable; the old value is not current.",
            [new("Temperature", "19.2 °C", ObservationQuality.Unavailable, SampleTime.AddHours(-2))]) { Icon = PlaceIcon.Bed },
        new("utility", "Utility", PlaceState.Problem, "A sample water sensor reports a leak.",
            [new("Water leak", "Detected", ObservationQuality.Current, SampleTime), new("Humidity", "65 %", ObservationQuality.Stale, SampleTime.AddHours(-1))])
    ];
}

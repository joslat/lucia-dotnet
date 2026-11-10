using Lucia.Companion.Core;

namespace Lucia.Companion.App;

/// <summary>Static, synthetic examples only. No device discovery, inferred occupancy or service calls.</summary>
internal static class WorkbenchFixtures
{
    private static readonly DateTimeOffset SampleTime = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    public static IReadOnlyList<Place> Places { get; } =
    [
        new("living", "Living room", PlaceState.Quiet, "No recent change in this sample.",
            [new("Temperature", "21.4 °C", ObservationQuality.Current, SampleTime) { Role = ObservationRole.Temperature }, new("Lighting", "Off", ObservationQuality.Current, SampleTime) { Role = ObservationRole.Light }]) { Icon = PlaceIcon.Sofa, Zone = PlaceZone.Inside },
        new("garden", "Garden", PlaceState.Changed, "A contact reading changed in this sample.",
            [new("Gate contact", "Open", ObservationQuality.Current, SampleTime) { Role = ObservationRole.Contact }, new("Soil moisture", null, ObservationQuality.Unknown, null) { Role = ObservationRole.Moisture }]) { Icon = PlaceIcon.Garden, Zone = PlaceZone.Outside },
        new("kitchen", "Kitchen", PlaceState.Working, "Lucia is reading a sensor in this sample. No physical action.",
            [new("Temperature", "20.8 °C", ObservationQuality.Current, SampleTime) { Role = ObservationRole.Temperature }]) { Icon = PlaceIcon.Kitchen, Zone = PlaceZone.Inside },
        new("porch", "Porch", PlaceState.Waiting, "A sample request is waiting for a response.",
            [new("Front door contact", "Closed", ObservationQuality.Current, SampleTime) { Role = ObservationRole.Contact }, new("Door lock", null, ObservationQuality.Unknown, null)]) { Icon = PlaceIcon.Door, Zone = PlaceZone.Outside },
        new("bedroom", "Bedroom", PlaceState.Unavailable, "The sample source is unavailable; the old value is not current.",
            [new("Temperature", "19.2 °C", ObservationQuality.Unavailable, SampleTime.AddHours(-2)) { Role = ObservationRole.Temperature }]) { Icon = PlaceIcon.Bed, Zone = PlaceZone.Inside },
        new("utility", "Utility", PlaceState.Problem, "A sample water sensor reports a leak.",
            [new("Water leak", "Detected", ObservationQuality.Current, SampleTime) { Role = ObservationRole.Leak }, new("Humidity", "65 %", ObservationQuality.Stale, SampleTime.AddHours(-1)) { Role = ObservationRole.Humidity }]) { Zone = PlaceZone.OwnBuilding }
    ];
}

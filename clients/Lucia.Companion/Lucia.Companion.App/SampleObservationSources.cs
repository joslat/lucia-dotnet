using Lucia.Companion.Core;

namespace Lucia.Companion.App;

/// <summary>Visible synthetic discovery for the offline setup exercise. Not authorized household discovery.</summary>
public static class SampleObservationSources
{
    public static IReadOnlyList<ObservationSource> Create() => Array.AsReadOnly(new[]
    {
        new ObservationSource("sensor.sample_temperature", "Sample temperature sensor", [ObservationRole.Temperature]),
        new ObservationSource("sensor.sample_humidity", "Sample humidity sensor", [ObservationRole.Humidity]),
        new ObservationSource("sensor.sample_soil", "Sample garden moisture", [ObservationRole.Moisture]),
        new ObservationSource("binary_sensor.sample_gate", "Sample gate contact", [ObservationRole.Contact]),
        new ObservationSource("binary_sensor.sample_motion", "Sample motion report", [ObservationRole.Motion]),
        new ObservationSource("binary_sensor.sample_leak", "Sample water leak", [ObservationRole.Leak]),
        new ObservationSource("light.sample_lamp", "Sample light state", [ObservationRole.Light])
    });
}

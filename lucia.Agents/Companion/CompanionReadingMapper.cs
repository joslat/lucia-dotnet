using System.Globalization;
using System.Text.Json;
using lucia.HomeAssistant.Models;

namespace lucia.Agents.Companion;

/// <summary>Allowlisted report semantics and freshness, never raw HA states or attributes on the wire.</summary>
internal static class CompanionReadingMapper
{
    public static CompanionField? Field(HomeAssistantState state)
    {
        var domain = state.EntityId.Split('.')[0];
        var deviceClass = Attribute(state, "device_class");
        return domain switch
        {
            "light" => CompanionField.Light,
            "sensor" => deviceClass switch
            {
                "temperature" => CompanionField.Temperature, "humidity" => CompanionField.Humidity,
                "moisture" => CompanionField.Moisture, "illuminance" => CompanionField.Light, _ => null
            },
            "binary_sensor" => deviceClass switch
            {
                "door" or "window" or "opening" or "garage_door" => CompanionField.Contact,
                "motion" => CompanionField.Motion, "moisture" => CompanionField.Leak, "light" => CompanionField.Light,
                _ => null // Locks, occupancy, safety and generic on/off never acquire a guessed meaning.
            },
            _ => null
        };
    }

    public static CompanionReading Read(HomeAssistantState state, CompanionField field, DateTimeOffset now, TimeSpan maximumAge)
    {
        var reported = state.LastReported;
        if (reported is null && state.LastUpdated != default && state.LastUpdated.Kind != DateTimeKind.Unspecified)
        { reported = new DateTimeOffset(state.LastUpdated.ToUniversalTime()); }
        if (reported > now || reported < DateTimeOffset.UnixEpoch) { reported = null; }
        var quality = state.State == "unavailable" ? CompanionReadingQuality.Unavailable : CompanionReadingQuality.Unknown;
        double? number = null;
        bool? active = null;
        string? unit = null;
        if (state.State is not ("unavailable" or "unknown"))
        {
            if (state.EntityId.StartsWith("sensor.", StringComparison.Ordinal))
            {
                var candidateUnit = Attribute(state, "unit_of_measurement");
                if (double.TryParse(state.State, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
                    && ValidNumber(field, candidateUnit, value)) { number = value; unit = candidateUnit; }
            }
            else if (state.State is "on" or "off") { active = state.State == "on"; }
            if (number.HasValue || active.HasValue)
            { quality = reported is null ? CompanionReadingQuality.Unknown : now - reported <= maximumAge ? CompanionReadingQuality.Current : CompanionReadingQuality.Stale; }
        }
        // Untimed/future reports cannot become even a dated last-known value.
        if (reported is null) { number = null; active = null; unit = null; }
        return new(state.EntityId, field, quality, reported, number, active, unit);
    }

    private static bool ValidNumber(CompanionField field, string? unit, double value) => field switch
    {
        CompanionField.Temperature => unit switch { "°C" => value >= -273.15, "°F" => value >= -459.67, "K" => value >= 0, _ => false },
        CompanionField.Humidity or CompanionField.Moisture => unit == "%" && value is >= 0 and <= 100,
        CompanionField.Light => unit == "lx" && value >= 0,
        _ => false
    };
    public static string? Attribute(HomeAssistantState state, string name) => state.Attributes is not null && state.Attributes.TryGetValue(name, out var value)
        ? value switch { string text => text, JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(), _ => null } : null;
}

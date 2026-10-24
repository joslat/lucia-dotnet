using System.Globalization;
using lucia.HomeAssistant.Models;

namespace lucia.Agents.Companion;

/// <summary>Shared exact registry lookup rules for Home readings and reported target areas.</summary>
internal sealed class CompanionRegistryLocations
{
    public Dictionary<string, AreaRegistryEntry> Areas { get; }
    public Dictionary<string, EntityRegistryEntry> Entities { get; }
    private readonly Dictionary<string, DeviceRegistryEntry> devices;

    public CompanionRegistryLocations(AreaRegistryEntry[] areas, EntityRegistryEntry[] entities, DeviceRegistryEntry[] devices)
    {
        if (areas.Length > 2000 || entities.Length > 20_000 || devices.Length > 20_000)
        { throw new CompanionDataUnavailableException(tooLarge: true); }
        Areas = Unique(areas, item => item.AreaId, ValidId);
        Entities = Unique(entities, item => item.EntityId, CompanionEntityIdentifier.IsValid);
        this.devices = Unique(devices, item => item.Id, ValidId);
    }

    public string? ResolveArea(EntityRegistryEntry entity)
    {
        if (entity.DisabledBy is not null || entity.HiddenBy is not null) { return null; }
        var area = entity.AreaId; var deviceId = entity.DeviceId;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (deviceId is not null)
        {
            if (seen.Count >= 32 || !seen.Add(deviceId) || !devices.TryGetValue(deviceId, out var device) || device.DisabledBy is not null)
            { return null; }
            area ??= device.AreaId; deviceId = device.ParentDeviceId;
        }
        return area is not null && Areas.ContainsKey(area) ? area : null;
    }

    public static Dictionary<string, T> Unique<T>(IEnumerable<T> items, Func<T, string> id, Func<string, bool> valid) where T : class =>
        items.Where(item => item is not null && valid(id(item))).GroupBy(id, StringComparer.Ordinal)
            .Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

    private static bool ValidId(string value) => !string.IsNullOrEmpty(value) && value.Length <= 128
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.' or ':');

    public static string Label(string? value, string fallback)
    {
        var clean = new string((value ?? string.Empty).Where(character => !char.IsControl(character) && char.GetUnicodeCategory(character) != UnicodeCategory.Format).Take(200).ToArray()).Trim();
        return clean.Length > 0 ? clean : fallback[..Math.Min(200, fallback.Length)];
    }
}

using System.Text.Json.Serialization;

namespace lucia.HomeAssistant.Models;

/// <summary>Only the location/availability fields needed for read-only device inheritance.</summary>
public sealed class DeviceRegistryEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("area_id")]
    public string? AreaId { get; set; }
    [JsonPropertyName("parent_device_id")]
    public string? ParentDeviceId { get; set; }
    [JsonPropertyName("disabled_by")]
    public string? DisabledBy { get; set; }
}

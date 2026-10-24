using System.Text.Json;
using System.Text.Json.Serialization;
using lucia.Agents.Companion;

namespace lucia.AgentHost.Companion;

internal static class CompanionApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter<CompanionField>(allowIntegerValues: false), new JsonStringEnumConverter<CompanionReadingQuality>(allowIntegerValues: false) }
    };
    public static async Task<T?> ReadAsync<T>(HttpContext context)
    {
        if (!context.Request.HasJsonContentType()) { return default; }
        try { return await context.Request.ReadFromJsonAsync<T>(Options, context.RequestAborted).ConfigureAwait(false); }
        catch (Exception error) when (error is JsonException or ArgumentException) { return default; }
    }
}

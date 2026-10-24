using System.Text.Json;
using System.Text.Json.Serialization;

namespace lucia.Agents.Companion;

/// <summary>One strict metadata vocabulary for owner requests and durable layout documents.</summary>
public static class CompanionLayoutJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static CompanionLayout Read(JsonElement payload)
    {
        RequireUniqueProperties(payload);
        return payload.Deserialize<CompanionLayout>(Options) ?? throw new JsonException("A layout is required.");
    }

    public static void RequireUniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) { throw new JsonException("Duplicate metadata properties are not supported."); }
                RequireUniqueProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        { foreach (var item in element.EnumerateArray()) { RequireUniqueProperties(item); } }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true, MaxDepth = 32,
            Converters = { new JsonStringEnumConverter<CompanionField>(allowIntegerValues: false) }
        };
        options.MakeReadOnly(populateMissingResolver: true); return options;
    }
}

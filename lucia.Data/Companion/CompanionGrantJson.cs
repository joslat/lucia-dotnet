using System.Text.Json;
using System.Text.Json.Serialization;
using lucia.Agents.Companion;

namespace lucia.Data.Companion;

internal static class CompanionGrantJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter<CompanionField>(allowIntegerValues: false) }
    };
    public static string Serialize(CompanionGrant grant) => JsonSerializer.Serialize(grant, Options);
    public static CompanionGrant Deserialize(string json) => JsonSerializer.Deserialize<CompanionGrant>(json, Options)
        ?? throw new InvalidDataException("The saved companion grant is missing.");
}

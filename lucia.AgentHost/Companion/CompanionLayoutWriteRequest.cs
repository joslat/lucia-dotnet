using System.Text.Json;
using lucia.Agents.Companion;

namespace lucia.AgentHost.Companion;

public sealed record CompanionLayoutWriteRequest(string? ExpectedRevision, CompanionLayout Layout)
{
    public const int MaximumBodyBytes = 524_288;

    internal static async Task<CompanionLayoutWriteRequest> ReadAsync(HttpContext context)
    {
        if (!context.Request.HasJsonContentType()) { throw new JsonException("JSON is required."); }
        using var buffer = new MemoryStream(); var chunk = new byte[8192];
        int count;
        while ((count = await context.Request.Body.ReadAsync(chunk, context.RequestAborted).ConfigureAwait(false)) != 0)
        {
            if (buffer.Length + count > MaximumBodyBytes) { throw new BadHttpRequestException("Layout body capacity exceeded.", StatusCodes.Status413PayloadTooLarge); }
            buffer.Write(chunk, 0, count);
        }
        using var document = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
        var root = document.RootElement; CompanionLayoutJson.RequireUniqueProperties(root);
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2
            || !root.TryGetProperty("expectedRevision", out var expected) || expected.ValueKind is not (JsonValueKind.Null or JsonValueKind.String)
            || !root.TryGetProperty("layout", out var layout)) { throw new JsonException("A checked layout write is required."); }
        var revision = expected.ValueKind == JsonValueKind.Null ? null : expected.GetString();
        if (revision is not null && (revision.Length != 32 || revision.Any(character => !char.IsAsciiDigit(character) && character is not (>= 'a' and <= 'f'))))
        { throw new JsonException("A valid checked revision is required."); }
        return new(revision, CompanionLayoutJson.Read(layout));
    }
}

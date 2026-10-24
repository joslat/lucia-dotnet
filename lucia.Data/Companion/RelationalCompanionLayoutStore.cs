using System.Data.Common;
using System.Text;
using System.Text.Json;
using lucia.Agents.Companion;
using static lucia.Data.Companion.CompanionSql;

namespace lucia.Data.Companion;

public sealed class RelationalCompanionLayoutStore(Func<CancellationToken, ValueTask<DbConnection>> openConnection, TimeProvider clock) : ICompanionLayoutStore
{
    private const int MaximumBytes = 524_288;

    public async Task<CompanionLayoutSnapshot?> ReadAsync(string homeId, CancellationToken cancellationToken = default)
    {
        CompanionIdentifier.Validate(homeId); cancellationToken.ThrowIfCancellationRequested();
        await using var connection = await openConnection(cancellationToken).ConfigureAwait(false);
        return (await ReadRowAsync(connection, homeId, cancellationToken).ConfigureAwait(false))?.Snapshot;
    }

    public async Task<CompanionLayoutSnapshot?> TrySaveAsync(CompanionLayout layout, string? expectedRevision, string ownerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout); CompanionIdentifier.Validate(ownerId);
        if (expectedRevision is not null && !ValidRevision(expectedRevision)) { throw new ArgumentException("A checked write requires a valid revision.", nameof(expectedRevision)); }
        cancellationToken.ThrowIfCancellationRequested();
        var json = JsonSerializer.Serialize(new { schemaVersion = 1, layout }, CompanionLayoutJson.Options);
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) { throw new ArgumentException("The shared layout exceeds its storage capacity.", nameof(layout)); }
        var revision = Guid.NewGuid().ToString("N"); var updatedAt = clock.GetUtcNow();
        await using var connection = await openConnection(cancellationToken).ConfigureAwait(false);
        string? previousJson = null;
        if (expectedRevision is not null)
        {
            var previous = await ReadRowAsync(connection, layout.HomeId, cancellationToken).ConfigureAwait(false);
            if (previous is null || previous.Value.Snapshot.Revision != expectedRevision) { return null; }
            previousJson = previous.Value.Json;
        }
        // The database statement is the linearization point across independent instances.
        // Comparing the checked bytes also preserves externally corrupted same-revision data.
        await using var command = Command(connection, null, expectedRevision is null ? """
            INSERT INTO companion_layouts(home_id, revision, document_json, updated_at, updated_by)
            VALUES (@home, @revision, @json, @at, @owner) ON CONFLICT(home_id) DO NOTHING;
            """ : """
            UPDATE companion_layouts SET revision = @revision, document_json = @json, updated_at = @at, updated_by = @owner
            WHERE home_id = @home AND revision = @expected AND document_json = @previous;
            """, ("home", layout.HomeId), ("revision", revision), ("json", json), ("at", updatedAt.ToUnixTimeMilliseconds()), ("owner", ownerId),
            ("expected", (object?)expectedRevision ?? DBNull.Value), ("previous", (object?)previousJson ?? DBNull.Value));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1
            ? new(revision, layout, DateTimeOffset.FromUnixTimeMilliseconds(updatedAt.ToUnixTimeMilliseconds()), ownerId) : null;
    }

    private static async Task<(CompanionLayoutSnapshot Snapshot, string Json)?> ReadRowAsync(DbConnection connection, string homeId, CancellationToken token)
    {
        await using var command = Command(connection, null, """
            SELECT revision, substr(document_json, 1, 524289), updated_at, updated_by
            FROM companion_layouts WHERE home_id = @home;
            """, ("home", homeId));
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false)) { return null; }
        try
        {
            var revision = reader.GetString(0); var json = reader.GetString(1);
            if (!ValidRevision(revision) || json.Length > MaximumBytes || Encoding.UTF8.GetByteCount(json) > MaximumBytes) { throw new InvalidDataException(); }
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            var root = document.RootElement;
            CompanionLayoutJson.RequireUniqueProperties(root);
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2
                || !root.TryGetProperty("schemaVersion", out var version) || !version.TryGetInt32(out var number) || number != 1
                || !root.TryGetProperty("layout", out var payload)) { throw new InvalidDataException(); }
            var layout = CompanionLayoutJson.Read(payload);
            if (layout.HomeId != homeId) { throw new InvalidDataException(); }
            var at = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(2));
            if (at <= DateTimeOffset.UnixEpoch) { throw new InvalidDataException(); }
            var owner = CompanionIdentifier.Validate(reader.GetString(3));
            return (new(revision, layout, at, owner), json);
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or InvalidDataException)
        { throw new InvalidDataException("The stored shared layout is invalid or unsupported. It was not replaced."); }
    }

    private static bool ValidRevision(string value) => value.Length == 32 && value.All(character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f');

}

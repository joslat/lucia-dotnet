using System.Data.Common;
using lucia.Agents.Companion;
using lucia.Data.Companion;
using Microsoft.Data.Sqlite;

namespace lucia.Tests.Data;

public sealed class CompanionLayoutStoreTests
{
    [Fact]
    public async Task CheckedCreateAndReplacePreserveTheOtherWritersDocumentAcrossReopen()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-layout-store-");
        try
        {
            var path = Path.Combine(directory.FullName, "synthetic.db");
            await InitializeAsync(path);
            var first = Store(path); var second = Store(path);
            Assert.Null(await first.ReadAsync("home-a"));
            var original = await first.TrySaveAsync(Layout("Original"), null, "owner-a"); Assert.NotNull(original);
            Assert.Null(await second.TrySaveAsync(Layout("Lost create"), null, "owner-b"));
            var replacement = await second.TrySaveAsync(Layout("Second"), original.Revision, "owner-b"); Assert.NotNull(replacement);
            Assert.NotEqual(original.Revision, replacement.Revision);
            Assert.Null(await first.TrySaveAsync(Layout("Stale"), original.Revision, "owner-a"));
            var reopened = await Store(path).ReadAsync("home-a"); Assert.NotNull(reopened);
            Assert.Equal("Second", reopened.Layout.Name); Assert.Equal("owner-b", reopened.UpdatedBy);
            Assert.Null(await first.ReadAsync("home-b"));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task IndependentConcurrentCreatesAndReplacementsHaveExactlyOneWinner()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-layout-race-");
        try
        {
            var path = Path.Combine(directory.FullName, "synthetic.db"); await InitializeAsync(path);
            var create = await Task.WhenAll(Task.Run(() => Store(path).TrySaveAsync(Layout("A"), null, "owner-a")), Task.Run(() => Store(path).TrySaveAsync(Layout("B"), null, "owner-b")));
            var baseline = Assert.Single(create, result => result is not null)!;
            var update = await Task.WhenAll(Task.Run(() => Store(path).TrySaveAsync(Layout("C"), baseline.Revision, "owner-a")), Task.Run(() => Store(path).TrySaveAsync(Layout("D"), baseline.Revision, "owner-b")));
            var winner = Assert.Single(update, result => result is not null)!;
            Assert.Equal(winner.Revision, (await Store(path).ReadAsync("home-a"))!.Revision);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task CorruptAndFutureDocumentsAreNotEmptyAndCannotBeOverwritten()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-layout-corrupt-");
        try
        {
            var path = Path.Combine(directory.FullName, "synthetic.db"); await InitializeAsync(path);
            var store = Store(path); var original = (await store.TrySaveAsync(Layout("Original"), null, "owner-a"))!;
            foreach (var broken in new[] { "not-json-PRIVATE", "{\"schemaVersion\":999,\"layout\":{}}", new string('x', 524_289) })
            {
                await using var connection = await OpenAsync(path); await using var command = connection.CreateCommand();
                command.CommandText = "UPDATE companion_layouts SET document_json = @json WHERE home_id = 'home-a';"; command.Parameters.Add(new SqliteParameter("json", broken)); await command.ExecuteNonQueryAsync();
                var failure = await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync("home-a")); Assert.DoesNotContain("PRIVATE", failure.ToString());
                await Assert.ThrowsAsync<InvalidDataException>(() => store.TrySaveAsync(Layout("Replacement"), original.Revision, "owner-a"));
                Assert.Null(await store.TrySaveAsync(Layout("Create"), null, "owner-a"));
                command.CommandText = "SELECT document_json FROM companion_layouts WHERE home_id = 'home-a';"; Assert.Equal(broken, await command.ExecuteScalarAsync());
            }
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task CancelledSaveDoesNotCreateOrReplaceTheDocument()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-layout-cancel-");
        try
        {
            var path = Path.Combine(directory.FullName, "synthetic.db"); await InitializeAsync(path);
            var store = Store(path); using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.TrySaveAsync(Layout("Cancelled"), null, "owner-a", cancelled.Token));
            Assert.Null(await store.ReadAsync("home-a"));
            var baseline = (await store.TrySaveAsync(Layout("Keep"), null, "owner-a"))!;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.TrySaveAsync(Layout("Cancelled"), baseline.Revision, "owner-a", cancelled.Token));
            Assert.Equal(baseline.Revision, (await store.ReadAsync("home-a"))!.Revision);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData("duplicate-name")]
    [InlineData("missing-name")]
    [InlineData("unknown-property")]
    [InlineData("foreign-home")]
    public async Task AmbiguousIncompleteOrForeignStoredMetadataFailsClosed(string defect)
    {
        var directory = Directory.CreateTempSubdirectory("lucia-layout-malformed-");
        try
        {
            var path = Path.Combine(directory.FullName, "synthetic.db"); await InitializeAsync(path);
            var store = Store(path); var baseline = (await store.TrySaveAsync(Layout("Original"), null, "owner-a"))!;
            await using var connection = await OpenAsync(path); await using var command = connection.CreateCommand();
            command.CommandText = "SELECT document_json FROM companion_layouts WHERE home_id='home-a';";
            var original = (string)(await command.ExecuteScalarAsync())!;
            var changed = defect switch
            {
                "duplicate-name" => original.Replace("\"name\":\"Original\"", "\"name\":\"Original\",\"name\":\"Shadow\"", StringComparison.Ordinal),
                "missing-name" => original.Replace("\"name\":\"Original\",", "", StringComparison.Ordinal),
                "unknown-property" => original.Replace("\"name\":\"Original\"", "\"name\":\"Original\",\"permissions\":\"owner\"", StringComparison.Ordinal),
                _ => original.Replace("\"homeId\":\"home-a\"", "\"homeId\":\"home-b\"", StringComparison.Ordinal)
            };
            Assert.NotEqual(original, changed);
            command.CommandText = "UPDATE companion_layouts SET document_json=@json WHERE home_id='home-a';"; command.Parameters.Add(new SqliteParameter("json", changed)); await command.ExecuteNonQueryAsync();
            await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync("home-a"));
            await Assert.ThrowsAsync<InvalidDataException>(() => store.TrySaveAsync(Layout("Do not overwrite"), baseline.Revision, "owner-a"));
            command.CommandText = "SELECT document_json FROM companion_layouts WHERE home_id='home-a';"; Assert.Equal(changed, await command.ExecuteScalarAsync());
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task OversizedSerializedMetadataIsRejectedBeforeOpeningADatabase()
    {
        var layout = new CompanionLayout("home-a", "http://synthetic-ha.invalid", "Home",
            [new("front", "Front", "Door", "porch")], [], Enumerable.Range(0, 2000)
                .Select(index => new CompanionLayoutBinding("front", "porch", "sensor." + new string('a', 235) + index.ToString("D4"), CompanionField.Temperature)).ToArray());
        var opened = false;
        var store = new RelationalCompanionLayoutStore(_ => { opened = true; throw new InvalidOperationException("Database must not open."); }, TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(() => store.TrySaveAsync(layout, null, "owner-a"));
        Assert.False(opened);
    }

    [Fact]
    public async Task ValidNonAsciiNamesRoundTripWithoutChangingTheirMeaning()
    {
        var directory = Directory.CreateTempSubdirectory("lucia-layout-unicode-");
        try
        {
            var path = Path.Combine(directory.FullName, "synthetic.db"); await InitializeAsync(path);
            var layout = new CompanionLayout("home-a", "http://synthetic-ha.invalid", "Chez nous 🌿",
                [new("front", "Entrée 🌷", "Door", "porch")], [], []);
            var store = Store(path); Assert.NotNull(await store.TrySaveAsync(layout, null, "owner-a"));
            var read = (await store.ReadAsync("home-a"))!;
            Assert.Equal(layout.Name, read.Layout.Name); Assert.Equal(layout.Places[0].Name, read.Layout.Places[0].Name);
        }
        finally { directory.Delete(recursive: true); }
    }

    internal static CompanionLayout Layout(string name) => new("home-a", "http://synthetic-ha.invalid", name,
        [new("front", "Front step", "Door", "porch")], [], [new("front", "porch", "sensor.porch", CompanionField.Temperature)]);
    internal static RelationalCompanionLayoutStore Store(string path) => new(async token => await OpenAsync(path, token), TimeProvider.System);
    private static async ValueTask<DbConnection> OpenAsync(string path, CancellationToken token = default)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        try { await connection.OpenAsync(token); return connection; } catch { await connection.DisposeAsync(); throw; }
    }
    private static async Task InitializeAsync(string path)
    {
        await using var connection = await OpenAsync(path); await using var command = connection.CreateCommand();
        command.CommandText = CompanionLayoutSchema.Sql;
        await command.ExecuteNonQueryAsync();
    }
}

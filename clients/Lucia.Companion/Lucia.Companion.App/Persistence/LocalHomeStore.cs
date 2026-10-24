using System.Text.Json;
using System.Text.Json.Serialization;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Persistence;

/// <summary>One local metadata file. Serial writes and same-directory replacement preserve the previous file on failure.</summary>
public sealed class LocalHomeStore(string path)
{
    private const int MaximumBytes = 1_048_576;
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        MaxDepth = 32,
        Converters = { new JsonStringEnumConverter<PlaceIcon>(allowIntegerValues: false), new JsonStringEnumConverter<HomeSection>(allowIntegerValues: false) }
    };
    private readonly string filePath = Path.GetFullPath(path);
    private readonly SemaphoreSlim writeGate = new(1, 1);

    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lucia", "Companion", "offline-home.json");

    public async Task<HomeProfile?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath)) { return null; }
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (stream.Length > MaximumBytes) { throw new InvalidDataException("The local setup file is too large."); }
        var profile = await JsonSerializer.DeserializeAsync<HomeProfile>(stream, Options, cancellationToken)
            ?? throw new JsonException("The local setup is empty.");
        profile.Validate();
        return profile;
    }

    public async Task SaveAsync(HomeProfile profile, CancellationToken cancellationToken = default)
    {
        profile.Validate();
        await writeGate.WaitAsync(cancellationToken);
        string? temporary = null;
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(profile, Options);
            if (bytes.Length > MaximumBytes) { throw new InvalidDataException("The local setup file is too large."); }
            var directory = Path.GetDirectoryName(filePath)!;
            Directory.CreateDirectory(directory);
            temporary = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, filePath, overwrite: true);
            temporary = null;
        }
        finally
        {
            try { if (temporary is not null && File.Exists(temporary)) { File.Delete(temporary); } }
            finally { writeGate.Release(); }
        }
    }
}

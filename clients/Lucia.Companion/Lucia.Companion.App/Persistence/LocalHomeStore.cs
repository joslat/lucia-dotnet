using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Persistence;

/// <summary>Local metadata with byte-revision checks, a cooperating-process lock and same-directory replacement.</summary>
public sealed class LocalHomeStore(string path)
{
    private const int MaximumBytes = 1_048_576;
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        MaxDepth = 32,
        Converters = { new JsonStringEnumConverter<PlaceIcon>(allowIntegerValues: false), new JsonStringEnumConverter<HomeSection>(allowIntegerValues: false),
            new JsonStringEnumConverter<ObservationRole>(allowIntegerValues: false) }
    };
    private readonly string filePath = Path.GetFullPath(path);

    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lucia", "Companion", "offline-home.json");

    public async Task<HomeProfile?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadSnapshotAsync(cancellationToken);
        if (snapshot.IsUnreadable) { throw new JsonException("The saved setup is not a supported home profile."); }
        return snapshot.Profile;
    }

    public async Task<LocalHomeSnapshot> LoadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        byte[] bytes;
        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (stream.Length > MaximumBytes) { throw new InvalidDataException("The local setup file is too large."); }
            bytes = new byte[(int)stream.Length];
            await stream.ReadExactlyAsync(bytes, cancellationToken);
            if (stream.ReadByte() != -1) { throw new IOException("The local setup changed while reading."); }
        }
        catch (FileNotFoundException) { return new(null, null); }
        catch (DirectoryNotFoundException) { return new(null, null); }

        var revision = Convert.ToHexString(SHA256.HashData(bytes));
        try
        {
            var profile = JsonSerializer.Deserialize<HomeProfile>(bytes, Options) ?? throw new JsonException("The local setup is empty.");
            profile.Validate();
            return new(revision, profile);
        }
        catch (Exception error) when (error is JsonException or ArgumentException) { return new(revision, null); }
    }

    /// <summary>Null expectedRevision permits creation only. No caller can request an unchecked overwrite.</summary>
    public async Task<string> SaveAsync(HomeProfile profile, string? expectedRevision, CancellationToken cancellationToken = default)
    {
        profile.Validate();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(profile, Options);
        if (bytes.Length > MaximumBytes) { throw new InvalidDataException("The local setup file is too large."); }
        var directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);
        await using var writeLock = await AcquireWriteLockAsync(cancellationToken);
        string? temporary = null;
        try
        {
            var current = await LoadSnapshotAsync(cancellationToken);
            if (!StringComparer.Ordinal.Equals(current.Revision, expectedRevision)) { throw new HomeSaveConflictException(); }
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
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        finally
        {
            if (temporary is not null && File.Exists(temporary)) { File.Delete(temporary); }
        }
    }

    private async Task<FileStream> AcquireWriteLockAsync(CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return new FileStream(filePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (timer.Elapsed < TimeSpan.FromSeconds(2)) { await Task.Delay(25, cancellationToken); }
        }
        // Keep the lock file. Deleting it could split cooperating writers across different lock files.
    }
}

using System.Text.Json;

namespace Lucia.Companion.App.Persistence;

/// <summary>A device preference, separate from household profiles and protected sessions.</summary>
public sealed class LocalMotionPreferenceStore(string path)
{
    private readonly string filePath = Path.GetFullPath(path);
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lucia", "Companion", "reduce-motion.json");

    public bool Load()
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (stream.Length > 64) { throw new InvalidDataException("The motion preference is too large."); }
            return JsonSerializer.Deserialize<bool>(stream);
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    public void Save(bool reduced)
    {
        var directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".motion-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, reduced);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) { File.Delete(temporary); }
        }
    }
}

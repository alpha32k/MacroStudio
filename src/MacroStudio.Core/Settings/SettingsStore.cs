using System.Text.Json;

namespace MacroStudio.Core.Settings;

/// <summary>Reads and writes <see cref="AppSettings"/> as a JSON file.</summary>
public sealed class SettingsStore(string filePath)
{
    public string FilePath { get; } = filePath;

    /// <summary>
    /// Loads the settings file. A missing or unreadable file gives the defaults;
    /// a broken file is kept next to the original as "*.broken" so nothing is lost silently.
    /// </summary>
    public AppSettings Load(Func<AppSettings> createDefault)
    {
        if (!File.Exists(FilePath))
        {
            return createDefault();
        }

        try
        {
            using FileStream stream = File.OpenRead(FilePath);
            AppSettings? settings = JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.AppSettings);
            return settings?.Normalize() ?? createDefault();
        }
        catch (JsonException)
        {
            // Kept for a look later; when even that copy fails, the program still starts with defaults.
            try
            {
                File.Copy(FilePath, FilePath + ".broken", overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            return createDefault();
        }
        catch (IOException)
        {
            return createDefault();
        }
        catch (UnauthorizedAccessException)
        {
            return createDefault();
        }
    }

    /// <summary>Writes to a temporary file first, so a crash never leaves a half-written settings file.</summary>
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string tempPath = FilePath + ".tmp";

        using (FileStream stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, settings, SettingsJsonContext.Default.AppSettings);
        }

        File.Move(tempPath, FilePath, overwrite: true);
    }
}

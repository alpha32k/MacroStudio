using System.Text.Json;
using System.Text.Json.Serialization;

namespace MacroStudio.Core.Playback;

/// <summary>
/// The last run of every macro, by macro id, in its own small file. Kept out of the macro files so that playing a
/// macro never rewrites it (and never fills its version history).
/// </summary>
public sealed class RunHistoryStore(string filePath)
{
    /// <summary>A missing or broken file gives an empty history; it only feeds the "last run" lines.</summary>
    public Dictionary<Guid, LastRun> Load()
    {
        try
        {
            using FileStream stream = File.OpenRead(filePath);
            return JsonSerializer.Deserialize(stream, RunHistoryJsonContext.Default.DictionaryGuidLastRun) ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public void Save(IReadOnlyDictionary<Guid, LastRun> runs)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        string tempPath = filePath + ".tmp";
        using (FileStream stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, runs.ToDictionary(), RunHistoryJsonContext.Default.DictionaryGuidLastRun);
        }

        File.Move(tempPath, filePath, overwrite: true);
    }
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(Dictionary<Guid, LastRun>))]
internal sealed partial class RunHistoryJsonContext : JsonSerializerContext;

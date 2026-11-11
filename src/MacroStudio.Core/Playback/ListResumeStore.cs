using System.Text.Json;
using System.Text.Json.Serialization;
using MacroStudio.Core.Macros;

namespace MacroStudio.Core.Playback;

/// <summary>The next data row of a macro, tied to the exact list snapshot it worked on.</summary>
public sealed class ListResumeStore(string directory)
{
    public int SuggestedRow(Guid macroId, PlaybackList list)
    {
        try
        {
            using FileStream stream = File.OpenRead(PathFor(macroId));
            ListResumeCheckpoint? checkpoint = JsonSerializer.Deserialize(stream, ListResumeJsonContext.Default.ListResumeCheckpoint);
            if (checkpoint is { Source: { } source, NextRow: > 0 } && checkpoint.NextRow <= list.Rows.Count
                && string.Equals(source.Path, list.Source.Path, StringComparison.OrdinalIgnoreCase)
                && source.HasHeader == list.Source.HasHeader && source.ContentHash == list.Source.ContentHash)
            {
                return checkpoint.NextRow;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A missing or unreadable checkpoint offers the first row.
        }

        return 1;
    }

    public void Save(Guid macroId, ListSource source, int nextRow)
    {
        Directory.CreateDirectory(directory);
        string path = PathFor(macroId);
        string tempPath = path + ".tmp";
        using (FileStream stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, new ListResumeCheckpoint(source, nextRow), ListResumeJsonContext.Default.ListResumeCheckpoint);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    private string PathFor(Guid macroId) => Path.Combine(directory, macroId.ToString("N") + ".json");
}

internal sealed record ListResumeCheckpoint(ListSource Source, int NextRow);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ListResumeCheckpoint))]
internal sealed partial class ListResumeJsonContext : JsonSerializerContext;

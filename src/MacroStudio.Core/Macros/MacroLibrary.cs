using System.Globalization;
using System.Text.Json;

namespace MacroStudio.Core.Macros;

/// <summary>The folder of .msmacro files. Each file is readable JSON named after its macro.</summary>
public sealed class MacroLibrary(string folder)
{
    public const string FileExtension = ".msmacro";
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;
    public const int KeptVersions = 10;
    public static readonly TimeSpan TrashKeepTime = TimeSpan.FromDays(30);
    private const int MaxFileNameLength = 80;
    private const string HistoryFolderName = ".history";
    private const string TrashFolderName = ".trash";
    private const string StampFormat = "yyyyMMdd-HHmmss-fff";

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public string Folder { get; } = folder;

    /// <summary>Reads every macro in the folder. Files that cannot be read are listed separately, not skipped silently.</summary>
    public (IReadOnlyList<MacroFile> Macros, IReadOnlyList<string> UnreadableFiles) LoadAll()
    {
        var macros = new List<MacroFile>();
        var unreadable = new List<string>();
        if (!Directory.Exists(Folder))
        {
            return (macros, unreadable);
        }

        foreach (string path in Directory.EnumerateFiles(Folder, "*" + FileExtension))
        {
            try
            {
                macros.Add(new MacroFile(path, Load(path), File.GetLastWriteTime(path)));
            }
            catch (Exception ex) when (ex is InvalidMacroFileException or IOException or UnauthorizedAccessException)
            {
                unreadable.Add(path);
            }
        }

        return (macros, unreadable);
    }

    public static MacroDefinition Load(string path)
    {
        if (new FileInfo(path).Length > MaxFileSizeBytes)
        {
            throw new InvalidMacroFileException($"File is larger than {MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        MacroDefinition? macro;
        try
        {
            using FileStream stream = File.OpenRead(path);
            macro = JsonSerializer.Deserialize(stream, MacroJsonContext.Default.MacroDefinition);
        }
        catch (JsonException ex)
        {
            throw new InvalidMacroFileException("File is not a valid macro.", ex);
        }

        return Checked(macro);
    }

    private static MacroDefinition Checked(MacroDefinition? macro)
    {
        if (macro is null)
        {
            throw new InvalidMacroFileException("File is empty.");
        }

        IReadOnlyList<string> problems = MacroValidator.Validate(macro);
        return problems.Count == 0 ? macro : throw new InvalidMacroFileException(string.Join(" ", problems));
    }

    /// <summary>Saves a new macro under a file name made from its name; returns the path.</summary>
    public string SaveNew(MacroDefinition macro)
    {
        Directory.CreateDirectory(Folder);
        string path = GetFreePath(macro.Name);
        Write(path, macro);
        return path;
    }

    /// <summary>
    /// Overwrites an existing macro file. The previous version is kept in the ".history" folder
    /// (the newest <see cref="KeptVersions"/>), so a wrong edit can be undone even after closing the app.
    /// </summary>
    public void Save(string path, MacroDefinition macro)
    {
        if (File.Exists(path))
        {
            string history = HistoryFolderOf(path);
            Directory.CreateDirectory(history);
            File.Copy(path, Path.Combine(history, DateTime.Now.ToString(StampFormat, CultureInfo.InvariantCulture) + FileExtension), overwrite: true);
            foreach (string old in Directory.GetFiles(history, "*" + FileExtension).OrderDescending(StringComparer.Ordinal).Skip(KeptVersions))
            {
                File.Delete(old);
            }
        }

        Write(path, macro);
    }

    /// <summary>Earlier versions of a macro, newest first.</summary>
    public IReadOnlyList<string> VersionsOf(string path)
    {
        string history = HistoryFolderOf(path);
        return Directory.Exists(history)
            ? Directory.GetFiles(history, "*" + FileExtension).OrderDescending(StringComparer.Ordinal).ToList()
            : [];
    }

    /// <summary>Gives a macro a new name; its file and saved versions are renamed with it. Returns the new path.</summary>
    public string Rename(string path, string newName)
    {
        MacroDefinition macro = Load(path) with { Name = newName };
        string newPath = GetFreePath(newName, currentPath: path);

        // The new name is written into the file where it is, then the file is moved in one step: when the move
        // fails (the file is open elsewhere) there is still exactly one file, never one under each name.
        Write(path, macro);
        if (!string.Equals(newPath, path, StringComparison.OrdinalIgnoreCase))
        {
            File.Move(path, newPath);
            MoveFolder(HistoryFolderOf(path), HistoryFolderOf(newPath));
        }

        return newPath;
    }

    /// <summary>Moves a macro and its saved versions to the trash, where it is kept for <see cref="TrashKeepTime"/>.</summary>
    public void MoveToTrash(string path)
    {
        string entry = Path.Combine(Folder, TrashFolderName, DateTime.Now.ToString(StampFormat, CultureInfo.InvariantCulture));
        Directory.CreateDirectory(entry);
        File.Move(path, Path.Combine(entry, Path.GetFileName(path)));
        MoveFolder(HistoryFolderOf(path), Path.Combine(entry, HistoryFolderName));
    }

    /// <summary>Deleted macros, most recently deleted first.</summary>
    public IReadOnlyList<TrashEntry> ListTrash()
    {
        string trash = Path.Combine(Folder, TrashFolderName);
        if (!Directory.Exists(trash))
        {
            return [];
        }

        var entries = new List<TrashEntry>();
        foreach (string entry in Directory.GetDirectories(trash))
        {
            bool isStamped = DateTime.TryParseExact(
                Path.GetFileName(entry), StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime deletedAt);
            if (isStamped && Directory.GetFiles(entry, "*" + FileExtension) is [string file])
            {
                entries.Add(new TrashEntry(entry, Path.GetFileNameWithoutExtension(file), deletedAt));
            }
        }

        return entries.OrderByDescending(entry => entry.DeletedAt).ToList();
    }

    /// <summary>Puts a deleted macro back. A macro of the same name made in the meantime keeps its file. Returns the path.</summary>
    public string Restore(TrashEntry entry)
    {
        string file = Directory.GetFiles(entry.Folder, "*" + FileExtension).Single();
        Directory.CreateDirectory(Folder);
        string path = GetFreePath(Path.GetFileNameWithoutExtension(file));
        File.Move(file, path);
        MoveFolder(Path.Combine(entry.Folder, HistoryFolderName), HistoryFolderOf(path));
        Directory.Delete(entry.Folder, recursive: true);
        return path;
    }

    /// <summary>Deletes for good what has been in the trash longer than <see cref="TrashKeepTime"/>.</summary>
    public void EmptyOldTrash(DateTime now)
    {
        foreach (TrashEntry entry in ListTrash().Where(entry => now - entry.DeletedAt > TrashKeepTime))
        {
            Directory.Delete(entry.Folder, recursive: true);
        }
    }

    private static void MoveFolder(string from, string to)
    {
        if (Directory.Exists(from) && !Directory.Exists(to))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            Directory.Move(from, to);
        }
    }

    private static void Write(string path, MacroDefinition macro)
    {
        IReadOnlyList<string> problems = MacroValidator.Validate(macro);
        if (problems.Count > 0)
        {
            throw new InvalidMacroFileException(string.Join(" ", problems));
        }

        // Written to a temporary file first, so a crash never leaves a half-written macro.
        string tempPath = path + ".tmp";
        using (FileStream stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, macro, MacroJsonContext.Default.MacroDefinition);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    private string HistoryFolderOf(string path) =>
        Path.Combine(Folder, HistoryFolderName, Path.GetFileNameWithoutExtension(path));

    /// <summary>"Rapor: Ocak/Şubat" becomes "Rapor Ocak Şubat"; an existing name gets " (2)", " (3)" and so on.</summary>
    /// <param name="currentPath">The macro's own file when renaming; it does not count as taken.</param>
    private string GetFreePath(string macroName, string? currentPath = null)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string cleaned = string.Join(' ', macroName.Split(invalid, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (cleaned.Length > MaxFileNameLength)
        {
            cleaned = cleaned[..MaxFileNameLength].TrimEnd();
        }

        string baseName = cleaned.Length > 0 ? cleaned : "Macro";

        // Windows does not allow device names such as CON or NUL as file names.
        if (ReservedNames.Contains(baseName))
        {
            baseName += "_";
        }

        string path = Path.Combine(Folder, baseName + FileExtension);
        for (int number = 2; File.Exists(path) && !string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase); number++)
        {
            path = Path.Combine(Folder, $"{baseName} ({number}){FileExtension}");
        }

        return path;
    }
}

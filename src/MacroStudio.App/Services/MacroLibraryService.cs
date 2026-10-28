using System.Collections.ObjectModel;
using System.IO;
using MacroStudio.Core.Macros;
using Serilog;

namespace MacroStudio.App.Services;

/// <summary>
/// The saved macros, newest first, as a list the bar and the pages can show, and every change to the folder.
/// Each change reloads the list. File errors (IOException, UnauthorizedAccessException) reach the caller,
/// which tells the user.
/// </summary>
public sealed class MacroLibraryService(MacroLibrary library)
{
    public ObservableCollection<MacroFile> Macros { get; } = [];

    /// <summary>Deleted macros, most recently deleted first.</summary>
    public IReadOnlyList<TrashEntry> Trash { get; private set; } = [];

    /// <summary>Files in the folder that are not valid macros; they are left where they are.</summary>
    public IReadOnlyList<string> UnreadableFiles { get; private set; } = [];

    public string Folder => library.Folder;

    public event EventHandler? Refreshed;

    public void Refresh()
    {
        (IReadOnlyList<MacroFile> macros, IReadOnlyList<string> unreadable) = library.LoadAll();
        foreach (string path in unreadable.Except(UnreadableFiles))
        {
            Log.Warning("Macro file could not be read and is not listed: {Path}", path);
        }

        UnreadableFiles = unreadable;
        Trash = library.ListTrash();
        Macros.Clear();
        foreach (MacroFile macro in macros.OrderByDescending(m => m.LastWriteTime))
        {
            Macros.Add(macro);
        }

        Refreshed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Called once at start-up: what has been in the trash for 30 days is deleted for good.</summary>
    public void EmptyOldTrash()
    {
        try
        {
            library.EmptyOldTrash(DateTime.Now);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Old macros could not be removed from the trash");
        }
    }

    /// <summary>Overwrites a macro; the previous version is kept (see <see cref="MacroLibrary.Save"/>).</summary>
    public void Save(string path, MacroDefinition macro)
    {
        library.Save(path, macro);
        Refresh();
    }

    /// <summary>The newest earlier version of a macro, or null when it was never overwritten.</summary>
    public MacroDefinition? LoadPreviousVersion(string path) =>
        library.VersionsOf(path) is [string newest, ..] ? MacroLibrary.Load(newest) : null;

    public MacroFile SaveNew(MacroDefinition macro)
    {
        string path = library.SaveNew(macro);
        Refresh();
        return Find(path);
    }

    public MacroFile Duplicate(MacroFile file, string copyName) =>
        SaveNew(file.Macro with { Id = Guid.NewGuid(), Name = copyName, CreatedAt = DateTimeOffset.Now, Shortcut = null });

    public MacroFile Rename(MacroFile file, string newName)
    {
        string path = library.Rename(file.Path, newName);
        Refresh();
        return Find(path);
    }

    public void MoveToTrash(MacroFile file)
    {
        library.MoveToTrash(file.Path);
        Log.Information("Macro moved to the trash: {Name}", file.Macro.Name);
        Refresh();
    }

    public void Restore(TrashEntry entry)
    {
        library.Restore(entry);
        Refresh();
    }

    /// <summary>Copies the macro file as it is; it is already valid, it was checked when saved.</summary>
    public static void Export(MacroFile file, string destination) => File.Copy(file.Path, destination, overwrite: true);

    private MacroFile Find(string path) =>
        Macros.First(m => string.Equals(m.Path, path, StringComparison.OrdinalIgnoreCase));
}

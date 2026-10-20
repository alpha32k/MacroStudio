namespace MacroStudio.Core.Macros;

/// <summary>A deleted macro in the trash folder.</summary>
/// <param name="Folder">The trash folder holding the macro file and its saved versions.</param>
/// <param name="Name">The macro's file name, without extension.</param>
public sealed record TrashEntry(string Folder, string Name, DateTime DeletedAt);

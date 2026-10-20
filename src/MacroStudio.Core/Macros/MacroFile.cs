namespace MacroStudio.Core.Macros;

/// <summary>A macro together with the file it was read from.</summary>
public sealed record MacroFile(string Path, MacroDefinition Macro, DateTime LastWriteTime);

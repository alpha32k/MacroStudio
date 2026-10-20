namespace MacroStudio.Core;

/// <summary>Per-user data folder: %LocalAppData%\MacroStudio. Needs no administrator rights.</summary>
public static class AppDataPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MacroStudio");

    public static string SettingsFile => Path.Combine(Root, "settings.json");

    public static string LogsDirectory => Path.Combine(Root, "logs");

    public static string MacrosDirectory => Path.Combine(Root, "macros");

    public static string RunHistoryFile => Path.Combine(Root, "runs.json");
}

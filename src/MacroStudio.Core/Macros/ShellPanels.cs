namespace MacroStudio.Core.Macros;

/// <summary>
/// Windows' own panels: Start, search, notifications and quick settings. They open and close with a key or a click,
/// often only after typing starts, and cannot be brought to the front like windows. Waiting for one would fail.
/// </summary>
public static class ShellPanels
{
    private const string PanelClassName = "Windows.UI.Core.CoreWindow";

    private static readonly HashSet<string> PanelProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "SearchApp", "SearchHost", "SearchUI", "StartMenuExperienceHost", "ShellExperienceHost",
    };

    // Hidden host windows of the Windows shell; they take the focus for a moment, for example when search closes.
    private static readonly HashSet<string> ShellHostClasses = new(StringComparer.Ordinal)
    {
        "ApplicationManager_DesktopShellWindow", "ApplicationManager_ImmersiveShellWindow",
    };

    public static bool IsShellPanel(string processName, string className) =>
        (className == PanelClassName && PanelProcesses.Contains(processName))
        || (string.Equals(processName, "explorer", StringComparison.OrdinalIgnoreCase) && ShellHostClasses.Contains(className));

    /// <summary>Also true for steps recorded before panels were told apart from windows.</summary>
    public static bool IsShellPanel(WindowTarget? target) =>
        target is not null && IsShellPanel(target.ProcessName, target.ClassName);
}

namespace MacroStudio.Core.Macros;

public static class BrowserWindow
{
    public static bool IsSupported(string processName, string className) =>
        className == "Chrome_WidgetWin_1" && (string.Equals(processName, "chrome", StringComparison.OrdinalIgnoreCase)
            || string.Equals(processName, "msedge", StringComparison.OrdinalIgnoreCase));
}

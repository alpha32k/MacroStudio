using Microsoft.Win32;

namespace MacroStudio.Native.WindowsSettings;

/// <summary>"Start with Windows" through the per-user Run key; needs no administrator rights.</summary>
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MacroStudio";

    public static bool IsEnabled(string executablePath)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is string command
            && string.Equals(command, Quote(executablePath), StringComparison.OrdinalIgnoreCase);
    }

    public static void Enable(string executablePath)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(ValueName, Quote(executablePath));
    }

    public static void Disable()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string Quote(string path) => $"\"{path}\"";
}

using Microsoft.Win32;

namespace MacroStudio.Native.WindowsSettings;

public static class WindowsAppearance
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>The "app mode" in Windows settings. Windows 10 before 1809 has no such value: light.</summary>
    public static bool AppsUseLightTheme()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
        return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
    }
}

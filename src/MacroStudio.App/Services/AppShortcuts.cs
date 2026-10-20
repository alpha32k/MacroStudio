using System.Windows.Input;
using MacroStudio.Core.Input;

namespace MacroStudio.App.Services;

/// <summary>
/// Macro Studio's own system-wide shortcuts. They are never recorded into a macro and do not count as
/// "the user touched the keyboard" while a macro plays.
/// </summary>
public static class AppShortcuts
{
    public static AppShortcut OpenMainWindow { get; } = new(ModifierKeys.Control | ModifierKeys.Alt, Key.M);

    public static AppShortcut ToggleRecording { get; } = new(ModifierKeys.Control | ModifierKeys.Alt, Key.R);

    public static AppShortcut PlayPause { get; } = new(ModifierKeys.Control | ModifierKeys.Alt, Key.P);

    /// <summary>
    /// Registered only while a macro plays: in Remote Desktop windows Ctrl+Alt+End stands for Ctrl+Alt+Del,
    /// and holding it all day would take it away from them.
    /// </summary>
    public static AppShortcut EmergencyStop { get; } = new(ModifierKeys.Control | ModifierKeys.Alt, Key.End);

    public static IReadOnlySet<KeyShortcut> All { get; } = new HashSet<KeyShortcut>
    {
        OpenMainWindow.ToKeyShortcut(),
        ToggleRecording.ToKeyShortcut(),
        PlayPause.ToKeyShortcut(),
        EmergencyStop.ToKeyShortcut(),
    };
}

using static MacroStudio.Native.Interop.NativeMethods;

namespace MacroStudio.Native.Input;

/// <summary>System-wide shortcuts through RegisterHotKey; no keyboard hook is needed for them.</summary>
public static class HotkeyInterop
{
    public const int WM_HOTKEY = 0x0312;

    /// <returns>False when another program already uses the same shortcut.</returns>
    public static bool Register(nint hwnd, int id, HotkeyModifiers modifiers, int virtualKey) =>
        RegisterHotKey(hwnd, id, (uint)modifiers, (uint)virtualKey);

    public static void Unregister(nint hwnd, int id) => UnregisterHotKey(hwnd, id);
}

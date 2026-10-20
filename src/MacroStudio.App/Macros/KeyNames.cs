using System.Globalization;
using System.Windows.Input;
using MacroStudio.Core.Input;

namespace MacroStudio.App.Macros;

/// <summary>Key names as printed on keyboards (Enter, Esc, ↓, F2), which are the same in Turkish and English.</summary>
public static class KeyNames
{
    public static string Of(int virtualKey)
    {
        Key key = KeyInterop.KeyFromVirtualKey(virtualKey);
        return key switch
        {
            Key.Return => "Enter",
            Key.Escape => "Esc",
            Key.Back => "Backspace",
            Key.Space => "Space",
            Key.Up => "↑",
            Key.Down => "↓",
            Key.Left => "←",
            Key.Right => "→",
            Key.PageUp => "Page Up",
            Key.PageDown => "Page Down",
            Key.LeftAlt or Key.RightAlt => "Alt",
            Key.LWin or Key.RWin => "Win",
            >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(CultureInfo.InvariantCulture),
            >= Key.NumPad0 and <= Key.NumPad9 => "Num " + ((int)(key - Key.NumPad0)).ToString(CultureInfo.InvariantCulture),
            _ => key.ToString(),
        };
    }

    /// <summary>For example "Ctrl+Shift+S".</summary>
    public static string WithModifiers(KeyModifiers modifiers, int virtualKey) => ModifierPrefix(modifiers) + Of(virtualKey);

    /// <summary>For example "Ctrl+Shift+"; empty when no modifier is held.</summary>
    public static string ModifierPrefix(KeyModifiers modifiers) =>
        string.Concat(
            modifiers.HasFlag(KeyModifiers.Control) ? "Ctrl+" : "",
            modifiers.HasFlag(KeyModifiers.Alt) ? "Alt+" : "",
            modifiers.HasFlag(KeyModifiers.Shift) ? "Shift+" : "",
            modifiers.HasFlag(KeyModifiers.Windows) ? "Win+" : "");
}

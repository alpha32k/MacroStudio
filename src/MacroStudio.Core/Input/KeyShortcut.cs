using System.Text.Json.Serialization;

namespace MacroStudio.Core.Input;

/// <summary>A key combination such as Ctrl+Alt+R.</summary>
public readonly record struct KeyShortcut(KeyModifiers Modifiers, int VirtualKey)
{
    /// <summary>
    /// Usable as a system-wide shortcut: one real key with Ctrl, Alt or Win. Shift alone would steal capital
    /// letters from every program, and a bare key would steal the key itself.
    /// </summary>
    [JsonIgnore]
    public bool IsUsable =>
        VirtualKey is > 0 and < 0xFF
        && !VirtualKeys.IsModifier(VirtualKey)
        && !VirtualKeys.IsLock(VirtualKey)
        && (Modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Windows)) != KeyModifiers.None;
}

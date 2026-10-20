using System.Windows.Input;
using MacroStudio.Core.Input;

namespace MacroStudio.App.Services;

/// <summary>A system-wide shortcut of Macro Studio, such as Ctrl+Alt+R.</summary>
public sealed record AppShortcut(ModifierKeys Modifiers, Key Key)
{
    public KeyShortcut ToKeyShortcut()
    {
        KeyModifiers modifiers = KeyModifiers.None;
        if (Modifiers.HasFlag(ModifierKeys.Control))
        {
            modifiers |= KeyModifiers.Control;
        }

        if (Modifiers.HasFlag(ModifierKeys.Alt))
        {
            modifiers |= KeyModifiers.Alt;
        }

        if (Modifiers.HasFlag(ModifierKeys.Shift))
        {
            modifiers |= KeyModifiers.Shift;
        }

        if (Modifiers.HasFlag(ModifierKeys.Windows))
        {
            modifiers |= KeyModifiers.Windows;
        }

        return new KeyShortcut(modifiers, KeyInterop.VirtualKeyFromKey(Key));
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        parts.Add(Key.ToString());
        return string.Join('+', parts);
    }
}

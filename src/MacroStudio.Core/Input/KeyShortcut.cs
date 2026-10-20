namespace MacroStudio.Core.Input;

/// <summary>A key combination such as Ctrl+Alt+R.</summary>
public readonly record struct KeyShortcut(KeyModifiers Modifiers, int VirtualKey);

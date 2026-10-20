namespace MacroStudio.Core.Input;

/// <summary>Modifier keys held down during a key press or a click.</summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4,
    Windows = 8,
}
